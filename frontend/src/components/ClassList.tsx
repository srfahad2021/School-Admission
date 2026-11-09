import { useState } from "react";
import { api, errorMessage } from "../api";
import { useToast } from "../toast";
import type { ClassView } from "../types";

interface Props {
  classes: ClassView[] | null;
  loading: boolean;
  studentId: string | null;
  activeClassIds: Set<string>;
  onChanged: () => void;
}

export default function ClassList({ classes, loading, studentId, activeClassIds, onChanged }: Props) {
  const toast = useToast();
  const [busyId, setBusyId] = useState<string | null>(null);

  async function apply(c: ClassView) {
    if (!studentId) return;
    setBusyId(c.id);
    try {
      await api.apply(c.id, studentId);
      toast(`Seat held in "${c.name}". Pay the fee before the timer runs out.`, "success");
    } catch (e) {
      toast(errorMessage(e));
    } finally {
      setBusyId(null);
      onChanged(); // refresh seat counts and applications either way
    }
  }

  function label(c: ClassView) {
    if (busyId === c.id) return "Applying...";
    if (activeClassIds.has(c.id)) return "Applied";
    if (c.availableSeats <= 0) return "Full";
    return "Apply";
  }

  return (
    <section>
      <h2>Classes</h2>
      {!studentId && <p className="muted">Enter your 17-digit Birth Registration Number above to apply.</p>}
      {loading && !classes && <p className="muted">Loading classes...</p>}
      {classes && classes.length === 0 && <p className="muted">No classes yet. Create one above.</p>}
      {classes && classes.length > 0 && (
        <div style={{ overflowX: "auto" }}>
          <table>
            <thead>
              <tr><th>Class</th><th>Total</th><th>Held</th><th>Confirmed</th><th>Available</th><th></th></tr>
            </thead>
            <tbody>
              {classes.map((c) => (
                <tr key={c.id}>
                  <td>{c.name}</td>
                  <td>{c.totalSeats}</td>
                  <td>{c.heldSeats}</td>
                  <td>{c.confirmedSeats}</td>
                  <td><strong>{c.availableSeats}</strong></td>
                  <td>
                    <button onClick={() => apply(c)}
                      disabled={!studentId || busyId !== null || c.availableSeats <= 0 || activeClassIds.has(c.id)}>
                      {label(c)}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}