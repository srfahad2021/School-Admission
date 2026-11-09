import { useCallback, useEffect, useMemo, useState } from "react";
import { api } from "./api";
import { usePolling } from "./hooks";
import { ToastProvider } from "./toast";
import CreateClassForm from "./components/CreateClassForm";
import ClassList from "./components/ClassList";
import MyApplications from "./components/MyApplications";

const STORAGE_KEY = "studentId";
const ID_RE = /^\d{17}$/;
const POLL_MS = 4000;

function readStored(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? "";
  } catch {
    return "";
  }
}

function Main() {
  const [draft, setDraft] = useState(readStored);
  const studentId = ID_RE.test(draft) ? draft : null;

  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, draft);
    } catch {
      /* storage unavailable: the app still works, just without persistence */
    }
  }, [draft]);

  const classes = usePolling(api.listClasses, POLL_MS, "classes");
  const holds = usePolling(
    useCallback(() => api.myHolds(studentId!), [studentId]),
    POLL_MS,
    studentId,
  );

  const refreshClasses = classes.refresh;
  const refreshHolds = holds.refresh;
  const refreshAll = useCallback(() => {
    refreshClasses();
    refreshHolds();
  }, [refreshClasses, refreshHolds]);

  const activeClassIds = useMemo(
    () => new Set((holds.data ?? []).filter((h) => h.status === "HELD" || h.status === "CONFIRMED").map((h) => h.classId)),
    [holds.data],
  );

  const serverProblem = classes.error ?? holds.error;
  const invalid = draft.length > 0 && !studentId;

  return (
    <>
      <header className="top">
        <div className="container">
          <strong>School Admission System</strong>
          <div>
            <input
              className={invalid ? "invalid" : ""}
              placeholder="17-digit Birth Registration Number"
              inputMode="numeric"
              maxLength={17}
              value={draft}
              onChange={(e) => setDraft(e.target.value.replace(/\D/g, ""))}
              aria-label="Birth Registration Number"
            />
            <div className="hint">
              {invalid ? `${draft.length}/17 digits. It must be exactly 17.` : studentId ? "ID accepted" : "Required to apply"}
            </div>
          </div>
        </div>
      </header>

      {serverProblem && (
        <div className="banner">{serverProblem} We will keep retrying automatically.</div>
      )}

      <main className="container">
        <CreateClassForm onCreated={classes.refresh} />
        <ClassList
          classes={classes.data}
          loading={classes.loading}
          studentId={studentId}
          activeClassIds={activeClassIds}
          onChanged={refreshAll}
        />
        <MyApplications holds={holds.data} loading={holds.loading} studentId={studentId} onChanged={refreshAll} />
      </main>
    </>
  );
}

export default function App() {
  return (
    <ToastProvider>
      <Main />
    </ToastProvider>
  );
}