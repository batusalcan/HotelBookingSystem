import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

export default function LoginPage() {
  const { signIn } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ email: '', password: '' })
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    setLoading(true)
    const { error: err } = await signIn(form.email, form.password)
    setLoading(false)
    if (err) {
      setError('Invalid email or password. Please try again.')
    } else {
      navigate('/')
    }
  }

  return (
    <div className="min-h-[80vh] flex items-center justify-center px-4 bg-slate-50">
      <div className="bg-white rounded-2xl border border-slate-100 shadow-sm p-10 w-full max-w-sm">
        <div className="text-center mb-8">
          <div className="text-3xl font-extrabold text-teal-700 mb-1">Hotels.com</div>
          <p className="text-slate-400 text-sm">Sign in to unlock exclusive member deals</p>
        </div>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Email</label>
            <input
              type="email"
              required
              autoComplete="email"
              value={form.email}
              onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
              className="border border-slate-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-slate-50"
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Password</label>
            <input
              type="password"
              required
              autoComplete="current-password"
              value={form.password}
              onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
              className="border border-slate-200 rounded-xl px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-teal-400 bg-slate-50"
            />
          </div>

          {error && (
            <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl p-3 text-sm">
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={loading}
            className="bg-teal-600 text-white py-3 rounded-xl font-bold hover:bg-teal-700 disabled:opacity-50 transition shadow-sm mt-1"
          >
            {loading ? 'Signing in...' : 'Sign In'}
          </button>
        </form>

        <div className="mt-6 text-center">
          <div className="bg-teal-50 border border-teal-100 rounded-xl p-3 text-xs text-teal-700">
            🎉 Members get <strong>15% off</strong> every booking
          </div>
        </div>

        <div className="mt-4 text-center text-xs text-slate-400 flex flex-col gap-2">
          <p>
            Don't have an account?{' '}
            <Link to="/signup" className="text-teal-600 font-semibold hover:underline">Sign Up</Link>
          </p>
          <Link to="/" className="text-slate-400 hover:underline">← Back to home</Link>
        </div>
      </div>
    </div>
  )
}
