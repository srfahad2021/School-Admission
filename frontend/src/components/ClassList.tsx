import { useState } from "react";
import { api, errorMessage } from "../api";
import { useToast } from "../toast";
import type { ClassView } from "../types";
import { t } from "../strings";

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
      toast(t.applied(c.name));
    } catch (e) {
      toast(errorMessage(e));
    } finally {
      setBusyId(null);
      onChanged(); // refresh seat counts and applications either way
    }
  }

  function label(c: ClassView) {
    if (busyId === c.id) return t.applying;
    if (activeClassIds.has(c.id)) return t.alreadyApplied;
    if (c.availableSeats <= 0) return t.full;
    return t.apply;
  }

  return (
    <section>
      <h2>{t.classListTitle}</h2>
      {!studentId && <p className="muted">{t.needId}</p>}
      {loading && !classes && <p className="muted">{t.loadingClasses}</p>}
      {classes && classes.length === 0 && <p className="muted">{t.noClasses}</p>}
      {classes && classes.length > 0 && (
        <div style={{ overflowX: "auto" }}>
          <table>
            <thead>
              <tr><th>{t.colClass}</th><th>{t.colTotal}</th><th>{t.colHeld}</th><th>{t.colConfirmed}</th><th>{t.colAvailable}</th><th></th></tr>
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