import { useEffect, useState } from "react";
import { API_URL } from "./config";

export default function App() {
  const [status, setStatus] = useState("Checking backend...");

  useEffect(() => {
    fetch(`${API_URL}/health`)
      .then((r) => r.json())
      .then((d) => setStatus(`Backend OK (hold duration: ${d.holdDurationSeconds}s)`))
      .catch(() => setStatus("Server is unreachable. Please try again."));
  }, []);

  return (
    <div style={{ padding: 24 }}>
      <h1>School Admission System</h1>
      <p>{status}</p>
    </div>
  );
}