import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

export default function Navbar() {
  const { session, isAdmin, signOut } = useAuth()
  const navigate = useNavigate()

  const handleSignOut = async () => {
    await signOut()
    navigate('/')
  }

  return (
    <nav className="bg-teal-800 text-white shadow-md">
      <div className="max-w-7xl mx-auto px-4 py-3 flex items-center justify-between">
        <Link to="/" className="flex items-center gap-2">
          <span className="text-2xl font-extrabold tracking-tight text-white">Hotels</span>
          <span className="text-teal-300 font-extrabold text-2xl">.com</span>
        </Link>

        <div className="flex items-center gap-4 text-sm">
          {session && isAdmin && (
            <Link to="/admin" className="text-teal-200 hover:text-white font-medium transition">
              Admin Panel
            </Link>
          )}
          {session ? (
            <button
              onClick={handleSignOut}
              className="border border-white text-white px-4 py-1.5 rounded-full font-semibold hover:bg-teal-700 transition text-sm"
            >
              Sign Out
            </button>
          ) : (
            <div className="flex items-center gap-2">
              <Link
                to="/signup"
                className="text-teal-200 hover:text-white font-semibold text-sm transition"
              >
                Sign Up
              </Link>
              <Link
                to="/login"
                className="border border-white text-white px-4 py-1.5 rounded-full font-semibold hover:bg-teal-700 transition text-sm"
              >
                Sign In
              </Link>
            </div>
          )}
        </div>
      </div>
    </nav>
  )
}
