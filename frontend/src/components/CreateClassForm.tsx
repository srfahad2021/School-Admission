import { useState } from "react";
import type { FormEvent } from "react";
import { api, errorMessage } from "../api";
import { useToast } from "../toast";
import { t } from "../strings";

export default function CreateClassForm({ onCreated }: { onCreated: () => void }) {
  const toast = useToast();
  const [name, setName] = useState("");
  const [seats, setSeats] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    const n = Number(seats);
    if (!name.trim()) return toast(t.enterClassName);
    if (!Number.isInteger(n) || n < 1 || n > 100000)
      return toast(t.seatsInvalid);

    setBusy(true);
    try {
      await api.createClass(name.trim(), n);
      toast(t.classCreated(name.trim()));
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
      <h2>{t.createClassTitle}</h2>
      <form className="row" onSubmit={submit}>
        <input placeholder={t.classNamePlaceholder} value={name}
               onChange={(e) => setName(e.target.value)} maxLength={200} style={{ flex: 1, minWidth: 220 }} />
        <input placeholder={t.seatsPlaceholder} type="number" min={1} value={seats}
               onChange={(e) => setSeats(e.target.value)} style={{ width: 110 }} />
        <button type="submit" disabled={busy}>{busy ? t.creating : t.create}</button>
      </form>
    </section>
  );
}