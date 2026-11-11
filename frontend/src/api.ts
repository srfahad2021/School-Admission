import { API_URL } from "./config";
import type { ClassView, MyHold } from "./types";
import { t, translateError } from "./strings";

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

// Server time minus browser time. The "Date" header only has 1-second precision,
// so add half a second to land in the middle of that second.
let clockOffsetMs = 0;
export const serverNow = () => Date.now() + clockOffsetMs;


export function errorMessage(e: unknown): string {
  return e instanceof Error ? e.message : t.genericError;
}

interface Options {
  method?: string;
  body?: unknown;
  studentId?: string;
}

async function request<T>(path: string, opts: Options = {}): Promise<T> {
  const headers: Record<string, string> = {};
  if (opts.body !== undefined) headers["Content-Type"] = "application/json";
  if (opts.studentId) headers["X-Student-Id"] = opts.studentId;

  let res: Response;
  try {
    res = await fetch(`${API_URL}${path}`, {
      method: opts.method ?? "GET",
      headers,
      body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
    });
  } catch {
    throw new ApiError(0, t.unreachable);
  }

  const dateHeader = res.headers.get("Date");
  if (dateHeader) {
    const t = Date.parse(dateHeader);
    if (!Number.isNaN(t)) clockOffsetMs = t + 500 - Date.now();
  }

  if (res.status === 204) return undefined as T;

  let data: { error?: string } | null = null;
  try {
    data = await res.json();
  } catch {
    /* body was not JSON */
  }

  if (!res.ok) throw new ApiError(res.status, translateError(data?.error, res.status));
  return data as T;
}

const enc = encodeURIComponent;

export const api = {
  listClasses: () => request<ClassView[]>("/classes"),
  createClass: (name: string, totalSeats: number) =>
    request<{ id: string }>("/classes", { method: "POST", body: { name, totalSeats } }),
  apply: (classId: string, studentId: string) =>
    request<{ holdId: string }>(`/classes/${enc(classId)}/holds`, { method: "POST", studentId }),
  confirm: (holdId: string, studentId: string) =>
    request<{ status: string }>(`/holds/${enc(holdId)}/confirm`, { method: "POST", studentId }),
  cancel: (holdId: string, studentId: string) =>
    request<void>(`/holds/${enc(holdId)}`, { method: "DELETE", studentId }),
  myHolds: (studentId: string) => request<MyHold[]>("/me/holds", { studentId }),
};