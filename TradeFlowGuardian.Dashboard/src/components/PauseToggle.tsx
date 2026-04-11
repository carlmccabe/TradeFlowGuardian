// Pause endpoint (POST /api/status/pause + GET /api/status/filters) is not yet
// implemented in the backend. This button is disabled until the endpoint ships.

export function PauseToggle() {
  return (
    <button
      disabled
      title="Pause/resume trading — backend endpoint pending"
      className="flex items-center gap-2 px-5 py-2.5 rounded-xl font-semibold text-sm opacity-30 cursor-not-allowed bg-gray-700 text-gray-300"
    >
      <span className="h-2 w-2 rounded-full bg-gray-400" />
      Pause Trading
    </button>
  )
}
