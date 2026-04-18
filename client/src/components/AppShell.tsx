import { FiActivity, FiAward, FiBarChart2, FiBell, FiCheckSquare, FiClipboard, FiLayers, FiLogOut, FiMenu, FiPhoneCall, FiSearch, FiShield, FiUsers, FiMessageSquare, FiUser, FiFileText, FiEdit } from 'react-icons/fi';
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
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
  requests: <FiClipboard />,
  suggestions: <FiEdit />,
  phone: <FiPhoneCall />,
  profile: <FiUser />
};

export function AppShell({ children }: { children: ReactNode }) {
  const { user, navigation, section, setSection, logout, search, setSearch, error, clearError, notice, clearNotice, activityLogs, unreadActivityCount, markActivitiesRead } = useApp();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [notificationsOpen, setNotificationsOpen] = useState(false);
  const [searchDraft, setSearchDraft] = useState(search);
  const notificationRef = useRef<HTMLDivElement | null>(null);
  const isLight = false;

  const currentTitle = useMemo(() => {
    return navigation.find((item) => item.key === section)?.label ?? 'لوحة التحكم';
  }, [navigation, section]);

  const activityFormatter = useMemo(() => new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }), []);
  const latestActivity = useMemo(() => activityLogs.slice(0, 5), [activityLogs]);
  const unreadLabel = unreadActivityCount > 9 ? '9+' : String(unreadActivityCount);

  useEffect(() => {
    setSearchDraft(search);
  }, [search]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if (searchDraft !== search) {
        setSearch(searchDraft);
      }
    }, 180);

    return () => window.clearTimeout(timer);
  }, [searchDraft, search, setSearch]);

  useEffect(() => {
    if (typeof document === 'undefined') {
      return undefined;
    }

    if (mobileOpen) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }

    return () => {
      document.body.style.overflow = '';
    };
  }, [mobileOpen]);

  useEffect(() => {
    if (!notificationsOpen) {
      return undefined;
    }

    const handleClick = (event: MouseEvent) => {
      if (!notificationRef.current || notificationRef.current.contains(event.target as Node)) {
        return;
      }
      setNotificationsOpen(false);
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setNotificationsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClick);
    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('mousedown', handleClick);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [notificationsOpen]);

  const navigate = (value: SectionKey) => {
    if (value !== section) {
      setSearch('');
    }

    setSection(value);
    setMobileOpen(false);
  };

  const toggleNotifications = () => {
    setNotificationsOpen((current) => {
      if (!current && unreadActivityCount > 0) {
        markActivitiesRead();
      }
      return !current;
    });
  };

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-[290px_1fr]">
      {mobileOpen && (
        <button
          type="button"
          aria-label="Close navigation"
          className="fixed inset-0 z-30 bg-slate-950/60 backdrop-blur-sm lg:hidden"
          onClick={() => setMobileOpen(false)}
        />
      )}
      <aside className={`fixed inset-y-0 right-0 z-40 w-[84vw] max-w-xs border-l px-4 py-5 backdrop-blur-xl transition-transform duration-300 sm:w-72 sm:px-5 sm:py-6 lg:static lg:w-auto lg:translate-x-0 ${isLight ? 'border-slate-200 bg-slate-50/95 text-slate-900' : 'border-white/10 bg-slate-950/95 text-slate-100'} ${mobileOpen ? 'translate-x-0' : 'translate-x-full lg:translate-x-0'}`}>
        <div className="flex h-full flex-col overflow-y-auto pr-1 scrollbar-thin">
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
        </div>
      </aside>

      <div className="flex min-w-0 flex-col">
        <header className={`sticky top-0 z-30 border-b backdrop-blur-xl ${isLight ? 'border-slate-200 bg-white/90' : 'border-white/10 bg-slate-950/85'}`}>
          <div className="flex flex-wrap items-center gap-3 px-4 py-4 sm:px-6 lg:px-8">
            <button type="button" className={`order-1 rounded-2xl border p-3 transition lg:hidden ${isLight ? 'border-slate-200 text-slate-700 hover:bg-slate-100' : 'border-white/10 text-slate-200 hover:bg-white/5'}`} onClick={() => setMobileOpen(true)}>
              <FiMenu />
            </button>

            <div className="order-2 flex min-w-0 flex-1 flex-col">
              <p className={`text-xs uppercase tracking-[0.35em] ${isLight ? 'text-brand-600/75' : 'text-brand-300/75'}`}>{currentTitle}</p>
              <h2 className={`truncate text-lg font-bold ${isLight ? 'text-slate-900' : 'text-white'}`}>{user?.fullName ?? 'لوحة التحكم'}</h2>
            </div>

            <div ref={notificationRef} className="order-3 relative sm:order-4">
              <button
                type="button"
                onClick={toggleNotifications}
                aria-label="الإشعارات"
                aria-expanded={notificationsOpen}
                aria-controls="activity-notifications"
                className={`relative rounded-2xl border p-3 transition ${isLight ? 'border-slate-200 text-slate-700 hover:bg-slate-100' : 'border-white/10 text-slate-200 hover:bg-white/5'}`}
              >
                <FiBell />
                {unreadActivityCount > 0 && (
                  <span className="absolute -right-1 -top-1 flex h-5 min-w-[1.25rem] items-center justify-center rounded-full bg-rose-500 px-1 text-[10px] font-bold text-white">
                    {unreadLabel}
                  </span>
                )}
              </button>

              {notificationsOpen && (
                <div
                  id="activity-notifications"
                  className={`absolute left-1/2 z-40 mt-3 w-[min(20rem,85vw)] -translate-x-1/2 overflow-hidden rounded-3xl border shadow-2xl sm:left-auto sm:right-0 sm:translate-x-0 ${isLight ? 'border-slate-200 bg-white text-slate-900' : 'border-white/10 bg-slate-950 text-slate-100'}`}
                >
                  <div className={`flex items-center justify-between border-b px-4 py-3 ${isLight ? 'border-slate-200' : 'border-white/10'}`}>
                    <div>
                      <p className="text-sm font-bold">الإشعارات</p>
                      <p className={`text-xs ${isLight ? 'text-slate-500' : 'text-slate-400'}`}>آخر الأنشطة المسجلة</p>
                    </div>
                    {activityLogs.length > 0 && (
                      <span className={`rounded-full px-2 py-1 text-xs font-bold ${isLight ? 'bg-slate-100 text-slate-700' : 'bg-white/10 text-slate-200'}`}>
                        {activityLogs.length}
                      </span>
                    )}
                  </div>

                  <div className="max-h-80 overflow-y-auto scrollbar-thin">
                    {latestActivity.length === 0 ? (
                      <div className={`px-4 py-6 text-center text-sm ${isLight ? 'text-slate-500' : 'text-slate-400'}`}>
                        لا توجد إشعارات بعد.
                      </div>
                    ) : (
                      latestActivity.map((item) => (
                        <div key={item.id} className={`border-b px-4 py-3 last:border-b-0 ${isLight ? 'border-slate-200' : 'border-white/10'}`}>
                          <p className="text-sm font-bold">{item.title}</p>
                          <p className={`mt-1 text-xs leading-6 ${isLight ? 'text-slate-600' : 'text-slate-400'}`}>{item.description}</p>
                          <span className={`mt-2 inline-block text-[11px] ${isLight ? 'text-slate-500' : 'text-slate-500'}`}>
                            {activityFormatter.format(new Date(item.createdAtUtc))}
                          </span>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              )}
            </div>

            <button type="button" className={`order-4 rounded-2xl border p-3 transition ${isLight ? 'border-slate-200 text-slate-700 hover:bg-slate-100' : 'border-white/10 text-slate-200 hover:bg-white/5'} sm:order-5`} onClick={logout} aria-label="logout">
              <FiLogOut />
            </button>

            <div className="order-5 flex w-full items-center gap-3 sm:order-3 sm:w-auto sm:flex-1 sm:max-w-xl">
              <div className={`flex flex-1 items-center gap-3 rounded-2xl border px-4 py-3 ${isLight ? 'border-slate-200 bg-slate-50' : 'border-white/10 bg-white/5'}`}>
                <FiSearch className={isLight ? 'text-slate-500' : 'text-slate-400'} />
                <input
                  value={searchDraft}
                  onChange={(event) => setSearchDraft(event.target.value)}
                  placeholder="بحث داخل الأقسام الحالية"
                  className={`w-full bg-transparent text-sm outline-none placeholder:text-slate-500 ${isLight ? 'text-slate-900' : 'text-white'}`}
                />
              </div>
            </div>
          </div>
        </header>

        {notice && (
          <div className={`border-b px-4 py-3 text-sm sm:px-6 lg:px-8 ${
            notice.tone === 'success'
              ? 'border-emerald-400/25 bg-emerald-400/10 text-emerald-100'
              : notice.tone === 'warning'
              ? 'border-amber-400/25 bg-amber-400/10 text-amber-100'
              : 'border-sky-400/25 bg-sky-400/10 text-sky-100'
          }`}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="font-bold">{notice.title}</p>
                <p className="text-xs text-slate-100/90">{notice.description}</p>
              </div>
              <button type="button" className="rounded-full border border-white/20 px-3 py-1 text-xs font-bold text-white/90 transition hover:bg-white/10" onClick={clearNotice}>
                إغلاق
              </button>
            </div>
          </div>
        )}

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

        <main className="min-w-0 flex-1 overflow-x-hidden px-4 py-6 sm:px-6 lg:px-8">
          {children}
        </main>
      </div>
    </div>
  );
}
