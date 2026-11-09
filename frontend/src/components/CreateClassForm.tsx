import { useState } from "react";
import type { FormEvent } from "react";
import { api, errorMessage } from "../api";
import { useToast } from "../toast";

export default function CreateClassForm({ onCreated }: { onCreated: () => void }) {
  const toast = useToast();
  const [name, setName] = useState("");
  const [seats, setSeats] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    const n = Number(seats);
    if (!name.trim()) return toast("Please enter a class name.");
    if (!Number.isInteger(n) || n < 1 || n > 100000)
      return toast("Seat capacity must be a whole number between 1 and 100000.");

    setBusy(true);
    try {
      await api.createClass(name.trim(), n);
      toast(`Class "${name.trim()}" created.`, "success");
      setName("");
      setSeats("");
      onCreated();
    } catch (err) {
      toast(errorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section>
      <h2>Create Class (School Admin)</h2>
      <form className="row" onSubmit={submit}>
        <input placeholder="e.g. Class 6 - Morning Shift" value={name}
               onChange={(e) => setName(e.target.value)} maxLength={200} style={{ flex: 1, minWidth: 220 }} />
        <input placeholder="Seats" type="number" min={1} value={seats}
               onChange={(e) => setSeats(e.target.value)} style={{ width: 110 }} />
        <button type="submit" disabled={busy}>{busy ? "Creating..." : "Create"}</button>
      </form>
    </section>
  );
}