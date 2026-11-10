using System.Net;
using static SchoolAdmission.Tests.Api;
using System.Net.Http.Json;
namespace SchoolAdmission.Tests;

public class AdmissionTests
{
    // 1. THE MANDATORY CONCURRENCY TEST
    [Fact]
    public async Task Concurrent_applies_never_exceed_capacity()
    {
        await using var f = await ApiFactory.CreateAsync();
        var client = f.CreateClient();
        var classId = await CreateClass(client, 10);

        var responses = await Task.WhenAll(
            Enumerable.Range(1, 200).Select(i => Task.Run(() => Apply(client, classId, StudentId(i)))));

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(10, created);
        Assert.Equal(190, conflicts);

        var cls = await GetClass(client, classId);
        Assert.Equal(10, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("availableSeats").GetInt32());
        Assert.True(cls.GetProperty("heldSeats").GetInt32() +
                    cls.GetProperty("confirmedSeats").GetInt32() <= 10);
    }

    // 2. Idempotent confirm
    [Fact]
    public async Task Parallel_confirms_increment_confirmed_seats_once()
    {
        await using var f = await ApiFactory.CreateAsync();
        var client = f.CreateClient();
        var classId = await CreateClass(client, 5);

        var applied = await Apply(client, classId, StudentId(1));
        var holdId = (await Json(applied)).GetProperty("holdId").GetString()!;

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => Task.Run(() => Confirm(client, holdId, StudentId(1)))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var cls = await GetClass(client, classId);
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
    }

    // 3. Same student, same class: only one active application
    [Fact]
    public async Task Same_student_cannot_apply_twice_to_same_class()
    {
        await using var f = await ApiFactory.CreateAsync();
        var client = f.CreateClient();
        var classId = await CreateClass(client, 5);

        var first = await Apply(client, classId, StudentId(1));
        var second = await Apply(client, classId, StudentId(1));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Parallel_duplicate_applies_from_one_student_create_one_hold()
    {
        await using var f = await ApiFactory.CreateAsync();
        var client = f.CreateClient();
        var classId = await CreateClass(client, 5);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => Task.Run(() => Apply(client, classId, StudentId(1)))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, (await GetClass(client, classId)).GetProperty("heldSeats").GetInt32());
    }

    // 4. Expiry frees the seat with no API call, and expired holds can't be confirmed
    [Fact]
    public async Task Expired_hold_releases_seat_and_cannot_be_confirmed()
    {
        await using var f = await ApiFactory.CreateAsync(holdSeconds: 2);
        var client = f.CreateClient();
        var classId = await CreateClass(client, 1);

        var applied = await Apply(client, classId, StudentId(1));
        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);
        var holdId = (await Json(applied)).GetProperty("holdId").GetString()!;

        // Class is full while the hold is active
        Assert.Equal(HttpStatusCode.Conflict, (await Apply(client, classId, StudentId(2))).StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(4));

        var cls = await GetClass(client, classId);
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("availableSeats").GetInt32());

        var hold = await Json(await client.GetAsync($"/holds/{holdId}"));
        Assert.Equal("EXPIRED", hold.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.Conflict, (await Confirm(client, holdId, StudentId(1))).StatusCode);

        // The freed seat can be taken by someone else
        Assert.Equal(HttpStatusCode.Created, (await Apply(client, classId, StudentId(2))).StatusCode);
    }

    // 5. Ownership, cancel and validation
    [Fact]
    public async Task Another_student_cannot_confirm_my_hold()
    {
        await using var f = await ApiFactory.CreateAsync();
        var client = f.CreateClient();
        var classId = await CreateClass(client, 5);
        var holdId = (await Json(await Apply(client, classId, StudentId(1)))).GetProperty("holdId").GetString()!;

        Assert.Equal(HttpStatusCode.Forbidden, (await Confirm(client, holdId, StudentId(2))).StatusCode);
    }

    [Fact]
    public async Task Cancel_frees_the_seat()
    {
        await using var f = await ApiFactory.CreateAsync();
        var client = f.CreateClient();
        var classId = await CreateClass(client, 1);
        var holdId = (await Json(await Apply(client, classId, StudentId(1)))).GetProperty("holdId").GetString()!;

        var del = await client.SendAsync(Req(HttpMethod.Delete, $"/holds/{holdId}", StudentId(1)));
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var cls = await GetClass(client, classId);
        Assert.Equal(1, cls.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Invalid_input_returns_400()
    {
        await using var f = await ApiFactory.CreateAsync();
        var client = f.CreateClient();
        var classId = await CreateClass(client, 5);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/classes", new { name = "X", totalSeats = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Apply(client, classId, "123")).StatusCode);
    }

    // BONUS: two separate backend instances against the SAME database
    [Fact]
    public async Task Two_backend_instances_never_over_hold()
    {
        var dbName = "t_" + Guid.NewGuid().ToString("N");
        await using var a = await ApiFactory.CreateAsync(database: dbName);
        await using var b = await ApiFactory.CreateAsync(database: dbName);
        var ca = a.CreateClient();
        var cb = b.CreateClient();

        var classId = await CreateClass(ca, 10);

        var responses = await Task.WhenAll(Enumerable.Range(1, 200).Select(i =>
            Task.Run(() => Apply(i % 2 == 0 ? ca : cb, classId, StudentId(i)))));

        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(190, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var cls = await GetClass(cb, classId);
        Assert.Equal(10, cls.GetProperty("heldSeats").GetInt32());
    }
}