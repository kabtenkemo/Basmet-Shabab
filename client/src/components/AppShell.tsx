import { FiActivity, FiAward, FiBarChart2, FiCheckSquare, FiLayers, FiLogOut, FiMenu, FiSearch, FiShield, FiUsers, FiMessageSquare, FiUser, FiFileText, FiLightBulb } from 'react-icons/fi';
import { useMemo, useState, type ReactNode } from 'react';
import { useApp } from '../context/AppContext';
import type { SectionKey } from '../types';

const icons: Record<string, ReactNode> = {
  grid: <FiBarChart2 />,
  news: <FiFileText />,
  leaderboard: <FiAward />,
  users: <FiUsers />,
  check: <FiCheckSquare />,
  message: <FiMessageSquare />,
  activity: <FiActivity />,
  layers: <FiLayers />,
  chart: <FiShield />,
  suggestions: <FiLightBulb />,
  profile: <FiUser />
};

export function AppShell({ children }: { children: ReactNode }) {
  const { user, navigation, section, setSection, logout, search, setSearch, error, clearError } = useApp();
  const [mobileOpen, setMobileOpen] = useState(false);
  const isLight = false;

  const currentTitle = useMemo(() => {
    return navigation.find((item) => item.key === section)?.label ?? 'لوحة التحكم';
  }, [navigation, section]);

  const navigate = (value: SectionKey) => {
    setSection(value);
    setMobileOpen(false);
  };

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-[290px_1fr]">
      <aside className={`fixed inset-y-0 right-0 z-40 w-72 border-l px-5 py-6 backdrop-blur-xl transition-transform duration-300 lg:static lg:w-auto lg:translate-x-0 ${isLight ? 'border-slate-200 bg-slate-50/95 text-slate-900' : 'border-white/10 bg-slate-950/95 text-slate-100'} ${mobileOpen ? 'translate-x-0' : 'translate-x-full lg:translate-x-0'}`}>
        <div className="mb-8 flex items-center justify-between">
          <div>
            <p className={`text-xs font-semibold uppercase tracking-[0.35em] ${isLight ? 'text-brand-600/75' : 'text-brand-300/75'}`}>Basma Shabab Platform</p>
            <h1 className={`mt-2 text-xl font-extrabold ${isLight ? 'text-slate-900' : 'text-white'}`}>منصة بصمة شباب</h1>
          </div>
          <button className="lg:hidden" onClick={() => setMobileOpen(false)}>
            <FiMenu className="text-xl" />
          </button>
        </div>

        <div className={`mb-6 rounded-3xl border p-4 ${isLight ? 'border-slate-200 bg-white text-slate-900' : 'border-white/10 bg-white/5 text-slate-100'}`}>
          <p className={`text-xs ${isLight ? 'text-slate-500' : 'text-slate-400'}`}>مرحبًا</p>
          <p className={`mt-2 text-lg font-bold ${isLight ? 'text-slate-900' : 'text-white'}`}>{user?.fullName}</p>
          <p className={`text-sm ${isLight ? 'text-slate-600' : 'text-slate-300'}`}>{user?.role}</p>
          <p className={`mt-2 text-xs ${isLight ? 'text-slate-500' : 'text-slate-400'}`}>
            {user?.governorName ? `المحافظة: ${user.governorName}` : 'بدون محافظة'}
          </p>
          <p className={`text-xs ${isLight ? 'text-slate-500' : 'text-slate-400'}`}>
            {user?.committeeName ? `اللجنة: ${user.committeeName}` : 'بدون لجنة'}
          </p>
        </div>

        <nav className="space-y-2">
          {navigation.map((item) => (
            <button
              key={item.key}
              type="button"
              onClick={() => navigate(item.key)}
              className={`flex w-full items-center gap-3 rounded-2xl px-4 py-3 text-right text-sm font-semibold transition ${section === item.key ? 'bg-brand-500 text-slate-950 shadow-glow' : isLight ? 'text-slate-700 hover:bg-slate-100 hover:text-slate-900' : 'text-slate-300 hover:bg-white/5 hover:text-white'}`}
            >
              <span className="text-lg">{icons[item.icon] ?? <FiShield />}</span>
              <span>{item.label}</span>
            </button>
          ))}
        </nav>

        <div className={`mt-8 rounded-3xl border p-4 text-sm ${isLight ? 'border-slate-200 bg-white text-slate-600' : 'border-white/10 bg-white/5 text-slate-300'}`}>
          <p className={`font-bold ${isLight ? 'text-slate-900' : 'text-white'}`}>RBAC</p>
          <p className="mt-2 leading-6">الواجهة تعرض فقط الأقسام المتوافقة مع الدور الحالي والصلاحيات المرتبطة به.</p>
        </div>
      </aside>

      <div className="flex min-w-0 flex-col">
        <header className={`sticky top-0 z-30 border-b backdrop-blur-xl ${isLight ? 'border-slate-200 bg-white/90' : 'border-white/10 bg-slate-950/85'}`}>
          <div className="flex flex-wrap items-center gap-3 px-4 py-4 sm:px-6 lg:px-8">
            <button type="button" className={`rounded-2xl border p-3 transition lg:hidden ${isLight ? 'border-slate-200 text-slate-700 hover:bg-slate-100' : 'border-white/10 text-slate-200 hover:bg-white/5'}`} onClick={() => setMobileOpen(true)}>
              <FiMenu />
            </button>

            <div className="flex min-w-0 flex-1 flex-col">
              <p className={`text-xs uppercase tracking-[0.35em] ${isLight ? 'text-brand-600/75' : 'text-brand-300/75'}`}>{currentTitle}</p>
              <h2 className={`truncate text-lg font-bold ${isLight ? 'text-slate-900' : 'text-white'}`}>{user?.fullName ?? 'لوحة التحكم'}</h2>
            </div>

            <div className="flex w-full flex-1 items-center gap-3 sm:max-w-xl">
              <div className={`flex flex-1 items-center gap-3 rounded-2xl border px-4 py-3 ${isLight ? 'border-slate-200 bg-slate-50' : 'border-white/10 bg-white/5'}`}>
                <FiSearch className={isLight ? 'text-slate-500' : 'text-slate-400'} />
                <input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="بحث داخل الأقسام الحالية"
                  className={`w-full bg-transparent text-sm outline-none placeholder:text-slate-500 ${isLight ? 'text-slate-900' : 'text-white'}`}
                />
              </div>
            </div>

            <button type="button" className={`rounded-2xl border p-3 transition ${isLight ? 'border-slate-200 text-slate-700 hover:bg-slate-100' : 'border-white/10 text-slate-200 hover:bg-white/5'}`} onClick={logout} aria-label="logout">
              <FiLogOut />
            </button>
          </div>
        </header>

        {error && (
          <div className="border-b border-rose-400/20 bg-rose-400/10 px-4 py-3 text-sm text-rose-100 sm:px-6 lg:px-8">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <p>{error}</p>
              <button type="button" className="rounded-full border border-rose-300/20 px-3 py-1 text-xs font-bold text-rose-50 transition hover:bg-rose-400/10" onClick={clearError}>
                إغلاق
              </button>
            </div>
          </div>
        )}

        <main className="min-w-0 flex-1 px-4 py-6 sm:px-6 lg:px-8">
          {children}
        </main>
      </div>
    </div>
  );
}
