import { useEffect, useState } from 'react'

type Connection = {
  id: string
  status: 'Pending' | 'Connected'
  keyId: number | null
  consumerKey: string | null
  consumerSecret: string | null
  permissions: string | null
}

type ProductsResult = { status: number; products: string[] | null; error: unknown }

async function connect() {
  const res = await fetch('/api/connect', { method: 'POST' })
  const { authorizeUrl } = await res.json()
  window.location.assign(authorizeUrl)
}

function ReturnView() {
  const params = new URLSearchParams(window.location.search)
  const success = params.get('success') === '1'
  const id = params.get('user_id') ?? ''

  const [connection, setConnection] = useState<Connection | null>(null)
  const [result, setResult] = useState<ProductsResult | string | null>(null)

  useEffect(() => {
    if (success) fetch(`/api/connections/${id}`).then((r) => (r.ok ? r.json() : null)).then(setConnection)
  }, [success, id])

  async function test() {
    const res = await fetch(`/api/connections/${id}/products-with-variants`)
    setResult(res.ok ? await res.json() : `HTTP ${res.status}: ${await res.text()}`)
  }

  if (!success) return <p>Access denied by the merchant (connection {id}).</p>
  if (!connection) return <p>Loading connection {id}…</p>

  return (
    <>
      <p>Status: <b>{connection.status}</b></p>
      <dl>
        <dt>Key id</dt><dd>{connection.keyId}</dd>
        <dt>Permissions</dt><dd>{connection.permissions}</dd>
        <dt>Consumer key</dt><dd><code>{connection.consumerKey}</code></dd>
        <dt>Consumer secret</dt><dd><code>{connection.consumerSecret}</code></dd>
      </dl>
      <button onClick={test}>Test connection</button>
      {result && <pre>{JSON.stringify(result, null, 2)}</pre>}
      <p><a href="/">Start over</a></p>
    </>
  )
}

export default function App() {
  return (
    <main style={{ fontFamily: 'system-ui', padding: 24 }}>
      <h1>Woo Connect PoC</h1>
      {window.location.pathname === '/connect/return'
        ? <ReturnView />
        : <button onClick={connect}>Connect store</button>}
    </main>
  )
}
