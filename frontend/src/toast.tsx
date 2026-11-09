import { createContext, useCallback, useContext, useState } from "react";
import type { ReactNode } from "react";

type Kind = "error" | "success";
interface ToastItem { id: number; msg: string; kind: Kind }

const ToastContext = createContext<(msg: string, kind?: Kind) => void>(() => {});
export const useToast = () => useContext(ToastContext);

let nextId = 1;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);

  const show = useCallback((msg: string, kind: Kind = "error") => {
    const id = nextId++;
    setItems((xs) => [...xs, { id, msg, kind }]);
    setTimeout(() => setItems((xs) => xs.filter((x) => x.id !== id)), 5000);
  }, []);

  return (
    <ToastContext.Provider value={show}>
      {children}
      <div className="toasts" role="status" aria-live="polite">
        {items.map((t) => (
          <div key={t.id} className={`toast ${t.kind}`}>{t.msg}</div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}