import { useEffect, useState } from "react";
import { api, errorMessage } from "../api";
import { useServerNow } from "../hooks";
import { useToast } from "../toast";
import type { MyHold } from "../types";
import { t } from "../strings";

function fmt(ms: number) {
  const s = Math.ceil(ms / 1000);
  return `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
}

interface CardProps {
  hold: MyHold;
  studentId: string;
  now: number;
  onChanged: () => void;
}

function HoldCard({ hold, studentId, now, onChanged }: CardProps) {
  const toast = useToast();
  const [busy, setBusy] = useState(false);

  const remaining = Math.max(0, new Date(hold.expiresAt).getTime() - now);
  const expiredLocally = hold.status === "HELD" && remaining === 0;
  const status = expiredLocally ? "EXPIRED" : hold.status;

  // The moment the timer hits zero, ask the server for the real state.
  useEffect(() => {
    if (expiredLocally) onChanged();
  }, [expiredLocally, onChanged]);

  async function pay() {
    setBusy(true);
    try {
      await api.confirm(hold.holdId, studentId);
      toast(t.paid(hold.className), "success");
    } catch (e) {
      toast(errorMessage(e));
    } finally {
      setBusy(false);
      onChanged();
    }
  }

  async function cancel() {
    setBusy(true);
    try {
      await api.cancel(hold.holdId, studentId);
      toast(t.cancelled, "success");
    } catch (e) {
      toast(errorMessage(e));
    } finally {
      setBusy(false);
      onChanged();
    }
  }

  return (
    <div className="card">
      <div>
        <strong>{hold.className}</strong>{" "}
        <span className={`badge ${status}`}>{status}</span>
        {status === "HELD" && (
          <div>{t.timeLeft}<span className="timer">{fmt(remaining)}</span></div>
        )}
        {status === "CONFIRMED" && <div className="muted">{t.paidNote}</div>}
        {status === "EXPIRED" && <div className="muted">{t.expiredNote}</div>}
      </div>
      {status === "HELD" && (
        <div style={{ display: "flex", gap: 8 }}>
          <button className="pay" onClick={pay} disabled={busy || expiredLocally}>
            {busy ? t.payBusy : t.pay}
          </button>
          <button className="danger" onClick={cancel} disabled={busy || expiredLocally}>{t.cancel}</button>
        </div>
      )}
    </div>
  );
}

interface Props {
  holds: MyHold[] | null;
  loading: boolean;
  studentId: string | null;
  onChanged: () => void;
}

export default function MyApplications({ holds, loading, studentId, onChanged }: Props) {
  const now = useServerNow();
  return (
    <section>
      <h2>{t.myAppsTitle}</h2>
      {!studentId && <p className="muted">{t.myAppsNeedId}</p>}
      {studentId && loading && !holds && <p className="muted">{t.loading}</p>}
      {studentId && holds && holds.length === 0 && <p className="muted">{t.noApps}</p>}
      <div className="cards">
        {studentId && holds?.map((h) => (
          <HoldCard key={h.holdId} hold={h} studentId={studentId} now={now} onChanged={onChanged} />
        ))}
      </div>
    </section>
  );
}