import './App.css'

function App() {
  return (
    <main className="page">
      <header className="hero">
        <p className="eyebrow">Event-Driven Inventory Reorder Platform Expansion</p>
        <h1>Inventory Operations Dashboard</h1>
        <p>
          Operator-facing dashboard for inventory visibility, reorder workflow
          status, processing history, and system health.
        </p>
      </header>

      <section className="grid">
        <article className="card">
          <h2>Inventory</h2>
          <p>Inventory table and low-stock filtering will be added here.</p>
        </article>

        <article className="card">
          <h2>Reorder Workflow</h2>
          <p>Reorder status and processing history will be displayed here.</p>
        </article>

        <article className="card">
          <h2>System Health</h2>
          <p>API, processor, queue, and database health will be summarized here.</p>
        </article>

        <article className="card">
          <h2>Failed Processing</h2>
          <p>Failed or poison-message handling will be surfaced here.</p>
        </article>
      </section>
    </main>
  )
}

export default App