import { useEffect, useMemo, useState, type FormEvent, type ReactElement } from 'react';
import { FiActivity, FiArrowLeft, FiClock, FiDownload, FiEdit3, FiPlus, FiPrinter, FiSave, FiSend, FiTrash2, FiThumbsUp, FiThumbsDown, FiUserPlus } from 'react-icons/fi';
import { AppShell } from './components/AppShell';
import { Badge, Button, Card, EmptyState, Field, Input, Modal, PagedTable, Select, SectionTitle, StatCard, Textarea, type TableColumn } from './components/ui';
import { useApp } from './context/AppContext';
import { commentComplaint, createCommittee, deleteCommittee, escalateComplaint, getAuditLogs, getComplaint, getGovernorateCommittees, getGovernorates, getSuggestions, createSuggestion, updateCommitteeJoinVisibility, updateGovernorateJoinVisibility, voteSuggestion } from './api';
import type { AuditLogFilters, AuditLogItem, CommitteeCreateFormState, CommitteeOption, ComplaintCommentState, ComplaintDetail, ComplaintEscalateState, ComplaintFormState, ComplaintItem, ComplaintReviewState, GovernorateOption, ImportantContactCreateState, ImportantContactItem, MemberAdminItem, MemberCreateFormState, NewsCreateState, NewsItem, PointFormState, Role, SectionKey, SuggestionItem, SuggestionFormState, TaskAudienceType, TaskFormState, TaskItem, TeamJoinRequest, TeamJoinRequestCreateState, TeamJoinRequestReviewState } from './types';

/**
 * Extract birthDate (YYYY-MM-DD) from Egyptian National ID
 * National ID format: 14 digits, first 6 digits represent: YYMMDD
 * Example: 91051512345678 -> 1991-05-15
 */
function extractBirthDateFromNationalId(nationalId: string): string | null {
  const normalized = nationalId.replace(/\s+/g, '');
  
  if (normalized.length !== 14 || /[^0-9]/.test(normalized)) {
    return null;
  }
  
  const yearStr = normalized.substring(0, 2);
  const monthStr = normalized.substring(2, 4);
  const dayStr = normalized.substring(4, 6);
  
  const year = parseInt(yearStr, 10);
  const month = parseInt(monthStr, 10);
  const day = parseInt(dayStr, 10);
  
  // Egyptian IDs: 00-30 = 2000s, 31-99 = 1900s (but more modern interpretation varies)
  // Using common pattern: 00-current year's last 2 digits = 20XX
  const fullYear = year <= 30 ? 2000 + year : 1900 + year;
  
  if (month < 1 || month > 12 || day < 1 || day > 31) {
    return null;
  }
  
  const date = new Date(fullYear, month - 1, day);
  if (date.getMonth() !== month - 1 || date.getDate() !== day) {
    return null;
  }
  
  return `${fullYear}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

const emptyTask: TaskFormState = {
  title: '',
  description: '',
  dueDate: '',
  audienceType: 'All',
  targetRoles: [],
  targetMemberIds: [],
  isCompleted: false
};

const emptyComplaint: ComplaintFormState = {
  subject: '',
  message: '',
  priority: 'Medium'
};

const emptyMember: MemberCreateFormState = {
  fullName: '',
  email: '',
  nationalId: '',
  birthDate: '',
  role: 'CommitteeMember',
  governorateId: '',
  committeeId: ''
};

const emptyCommittee: CommitteeCreateFormState = {
  name: ''
};

const importantContactDomains = [
  'حكومي',
  'إعلام',
  'تعليم',
  'صحة',
  'قطاع خاص',
  'مجتمع مدني',
  'شباب ورياضة',
  'تقني'
];

const emptyImportantContact: ImportantContactCreateState = {
  fullName: '',
  phoneNumber: '',
  positionTitle: '',
  domain: ''
};

const emptyPoint: PointFormState = {
  amount: '5',
  reason: ''
};

const emptyNews: NewsCreateState = {
  title: '',
  content: '',
  audienceType: 'All',
  targetRoles: [],
  targetMemberIds: []
};

const emptySuggestion: SuggestionFormState = {
  title: '',
  description: ''
};

const emptyJoinRequest: TeamJoinRequestCreateState = {
  applicationType: 'GovernorateMembers',
  fullName: '',
  email: '',
  phoneNumber: '',
  nationalId: '',
  birthDate: '',
  governorateId: '',
  committeeId: '',
  motivation: '',
  experience: ''
};

const roleLabels: Record<Role, string> = {
  President: 'رئيس الكيان',
  VicePresident: 'مساعد الرئيس',
  CentralMember: 'عضو مركزية',
  GovernorCoordinator: 'منسق محافظة',
  GovernorCommitteeCoordinator: 'منسق لجنة',
  CommitteeMember: 'عضو لجنة'
};

const taskAudienceLabels: Record<TaskAudienceType, string> = {
  All: 'للجميع',
  Members: 'لأعضاء معينين',
  Roles: 'لمناصب معينة'
};

const permissionOptions = [
  { key: 'Users.Manage', label: 'إدارة الأعضاء' },
  { key: 'Roles.Manage', label: 'إدارة الأدوار' },
  { key: 'Points.Manage', label: 'تعديل النقاط' },
  { key: 'Complaints.Manage', label: 'إدارة الشكاوى' },
  { key: 'Dashboard.View', label: 'عرض لوحة التحكم' },
  { key: 'Members.Create.CentralMember', label: 'إنشاء عضو مركزية' },
  { key: 'Members.Create.GovernorCoordinator', label: 'إنشاء منسق محافظة' },
  { key: 'Members.Create.GovernorCommitteeCoordinator', label: 'إنشاء منسق لجنة' },
  { key: 'Members.Create.CommitteeMember', label: 'إنشاء عضو لجنة' }
] as const;

const permissionOptionsWithJoinRequests = [
  ...permissionOptions,
  { key: 'JoinRequests.Review', label: 'مراجعة طلبات الالتحاق' },
  { key: 'JoinRequests.Visibility.Manage', label: 'فتح/إغلاق التقديم على المحافظة' }
] as const;

const statusLabels: Record<string, string> = {
  Open: 'مفتوحة',
  InReview: 'قيد المراجعة',
  Resolved: 'تم الحل',
  Rejected: 'مرفوضة'
};

const priorityLabels: Record<string, string> = {
  Low: 'منخفضة',
  Medium: 'متوسطة',
  High: 'عالية'
};

const joinRequestStatusLabels: Record<string, string> = {
  Pending: 'قيد المراجعة',
  Reviewed: 'تمت المراجعة',
  Accepted: 'مقبول',
  Rejected: 'مرفوض'
};

const pageTitles: Record<SectionKey, { eyebrow: string; title: string; description: string }> = {
  overview: {
    eyebrow: 'Dashboard',
    title: 'لوحة متابعة شاملة بالعربية وبتنظيم هرمي واضح',
    description: 'عرض سريع للأعضاء والمهام والشكاوى والمتصدرين مع واجهة RTL جاهزة للعرض والاستخدام اليومي.'
  },
  leaderboard: {
    eyebrow: 'Leaderboard',
    title: 'المتصدرين حسب المناصب',
    description: 'عرض واضح يبين عدد الأعضاء وترتيبهم داخل كل منصب حتى يكون ظاهرًا للجميع.'
  },
  news: {
    eyebrow: 'News',
    title: 'إعلانات وأخبار الكيان',
    description: 'رسائل رسمية من رئيس الكيان أو مساعد الرئيس، موجهة للجميع أو لفئات محددة.'
  },
  joinrequests: {
    eyebrow: 'Applicants',
    title: 'طلبات المتقدمين للانضمام',
    description: 'متابعة طلبات الانضمام حسب المحافظة مع إمكانية القبول أو الرفض.'
  },
  members: {
    eyebrow: 'User Management',
    title: 'إدارة الأعضاء والصلاحيات',
    description: 'إنشاء أعضاء جدد، تغيير الأدوار، منح الصلاحيات، وتعديل النقاط من شاشة واحدة.'
  },
  studentclubs: {
    eyebrow: 'Student Clubs',
    title: 'النوادي الطلابية والأدوار التنظيمية',
    description: 'عرض مناصب النوادي الطلابية داخل النطاق الحالي (Club) بدون رئيس كيان أو مساعد رئيس أو عضو مركزية.'
  },
  tasks: {
    eyebrow: 'Task Management',
    title: 'إدارة المهام وتتبع الحالات',
    description: 'إنشاء مهام، تعديلها، حذفها، وتتبّع المهام المكتملة والمهام قيد التنفيذ.'
  },
  complaints: {
    eyebrow: 'Complaints',
    title: 'نظام شكاوى متدرج حسب الصلاحية',
    description: 'إرسال الشكوى ومراجعتها والإشراف عليها حسب الدور الحالي داخل النظام.'
  },
  auditlogs: {
    eyebrow: 'Audit Logs',
    title: 'سجل التدقيق والعمليات',
    description: 'عرض التغييرات المهمة على المستخدمين والمهام والشكاوى مع تتبع القيم القديمة والجديدة.'
  },
  committees: {
    eyebrow: 'Committees',
    title: 'إدارة اللجان ونطاقات العمل',
    description: 'واجهة جاهزة لإدارة اللجان وربطها بالمحافظات وتعيين المنسقين.'
  },
  importantcontacts: {
    eyebrow: 'Key Contacts',
    title: 'أرقام الشخصيات الهامة',
    description: 'قائمة موحدة للتواصل مع الشخصيات الهامة حسب المجال والمنصب.'
  },
  suggestions: {
    eyebrow: 'Suggestions',
    title: 'نظام المقترحات والتصويت',
    description: 'منصة لتقديم المقترحات والأفكار والتصويت عليها من قبل الأعضاء.'
  },
  reports: {
    eyebrow: 'Reports',
    title: 'تصدير التقارير والمراجعة العامة',
    description: 'تصدير سريع للملفات وفتح الطباعة بصيغة مناسبة لـ PDF من المتصفح.'
  },
  profile: {
    eyebrow: 'Profile',
    title: 'الملف الشخصي والإعدادات',
    description: 'معلومات العضو، الصلاحيات، النشاط الأخير، وخيارات المظهر.'
  }
};

const dateTimeFormatter = new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' });
const dateOnlyFormatter = new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium' });

function formatDate(value: string | null | undefined) {
  if (!value) {
    return '—';
  }

  return dateTimeFormatter.format(new Date(value));
}

function formatDateOnly(value: string | null | undefined) {
  if (!value) {
    return '—';
  }

  return dateOnlyFormatter.format(new Date(`${value}T00:00:00`));
}

function roleLabel(role: string) {
  return roleLabels[role as Role] ?? role;
}

function clubRoleLabel(role: Role) {
  if (role === 'GovernorCoordinator') {
    return 'منسق محافظة Club';
  }

  if (role === 'GovernorCommitteeCoordinator') {
    return 'منسق لجنة Club';
  }

  if (role === 'CommitteeMember') {
    return 'عضو لجنة Club';
  }

  return roleLabel(role);
}

function statusLabel(status: string) {
  return statusLabels[status] ?? status;
}

function priorityLabel(priority: string) {
  return priorityLabels[priority] ?? priority;
}

function audienceLabel(value: string) {
  if (value === 'All') return 'للجميع';
  if (value === 'Roles') return 'لأدوار محددة';
  if (value === 'Members') return 'لأعضاء محددين';
  return value;
}

function taskAudienceLabel(value: string) {
  return taskAudienceLabels[value as TaskAudienceType] ?? value;
}

function roleNeedsGovernorate(role: Role) {
  return role === 'GovernorCoordinator' || role === 'GovernorCommitteeCoordinator' || role === 'CommitteeMember';
}

function roleNeedsCommittee(role: Role) {
  return role === 'GovernorCommitteeCoordinator' || role === 'CommitteeMember';
}

function normalizeScopeName(value: string | null | undefined) {
  return value?.trim().toLowerCase() ?? '';
}

function isSameScopeName(left: string | null | undefined, right: string | null | undefined) {
  if (!left || !right) {
    return false;
  }

  return normalizeScopeName(left) === normalizeScopeName(right);
}

function isClubCommitteeName(value: string | null | undefined) {
  const normalized = normalizeScopeName(value);
  if (!normalized) {
    return false;
  }

  return normalized.includes('club')
    || normalized.includes('نادي')
    || normalized.includes('النوادي')
    || normalized.includes('طلابي');
}

function hasAtLeastTwoNameParts(value: string) {
  return value.trim().split(/\s+/).filter(Boolean).length >= 2;
}

function safeJsonParse(value: string | null | undefined) {
  if (!value) {
    return null;
  }

  try {
    return JSON.parse(value) as Record<string, unknown>;
  } catch {
    return null;
  }
}

function renderAuditDiff(log: AuditLogItem) {
  const oldValues = safeJsonParse(log.oldValuesJson);
  const newValues = safeJsonParse(log.newValuesJson);
  const keys = Array.from(new Set([...(oldValues ? Object.keys(oldValues) : []), ...(newValues ? Object.keys(newValues) : [])]));

  if (keys.length === 0) {
    return <p className="text-sm text-slate-400">لا توجد قيم مسجلة.</p>;
  }

  return (
    <div className="grid gap-3 md:grid-cols-2">
      {keys.map((key) => (
        <div key={key} className="rounded-2xl border border-white/10 bg-slate-950/50 p-3">
          <p className="text-xs font-semibold uppercase tracking-[0.25em] text-brand-300/80">{key}</p>
          <div className="mt-2 space-y-1 text-sm">
            <p className="text-slate-400">قبل: <span className="text-slate-200">{oldValues?.[key] !== undefined ? String(oldValues[key]) : '—'}</span></p>
            <p className="text-slate-400">بعد: <span className="text-slate-200">{newValues?.[key] !== undefined ? String(newValues[key]) : '—'}</span></p>
          </div>
        </div>
      ))}
    </div>
  );
}

function loginTitle() {
  return 'تسجيل الدخول إلى منصة بصمة شباب';
}

type PublicRoute = 'login' | 'join';

const sectionPathByKey: Record<SectionKey, string> = {
  overview: '/dashbourd',
  leaderboard: '/leaderboard',
  news: '/news',
  joinrequests: '/join-requests',
  members: '/members',
  studentclubs: '/student-clubs',
  tasks: '/tasks',
  complaints: '/complaints',
  auditlogs: '/audit-logs',
  committees: '/committees',
  importantcontacts: '/important-contacts',
  suggestions: '/suggestions',
  reports: '/reports',
  profile: '/profile'
};

const sectionByPath: Record<string, SectionKey> = {
  '/dashbourd': 'overview',
  '/dashboard': 'overview',
  '/overview': 'overview',
  '/leaderboard': 'leaderboard',
  '/news': 'news',
  '/join-requests': 'joinrequests',
  '/joinrequests': 'joinrequests',
  '/members': 'members',
  '/student-clubs': 'studentclubs',
  '/studentclubs': 'studentclubs',
  '/tasks': 'tasks',
  '/complaints': 'complaints',
  '/audit-logs': 'auditlogs',
  '/auditlogs': 'auditlogs',
  '/committees': 'committees',
  '/important-contacts': 'importantcontacts',
  '/importantcontacts': 'importantcontacts',
  '/suggestions': 'suggestions',
  '/reports': 'reports',
  '/profile': 'profile'
};

function normalizePathname(pathname: string): string {
  const normalized = pathname.trim().toLowerCase();

  if (!normalized || normalized === '/') {
    return '/';
  }

  return normalized.replace(/\/+$/, '');
}

function resolvePrivateSection(pathname: string): SectionKey {
  const normalizedPath = normalizePathname(pathname);
  return sectionByPath[normalizedPath] ?? 'overview';
}

function resolvePublicRoute(): PublicRoute {
  if (typeof window === 'undefined') {
    return 'login';
  }

  const pathname = normalizePathname(window.location.pathname);
  if (pathname === '/join') {
    return 'join';
  }

  const hash = window.location.hash.toLowerCase();
  return hash === '#/join' ? 'join' : 'login';
}

function LoginView({ onNavigateToJoin }: { onNavigateToJoin: () => void }) {
  const { loginUser, loading, error, clearError } = useApp();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [formError, setFormError] = useState('');
  const [capsLockDetected, setCapsLockDetected] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [rememberEmail, setRememberEmail] = useState(true);

  useEffect(() => {
    clearError();
  }, [clearError]);

  useEffect(() => {
    const rememberedEmail = localStorage.getItem('basma-remembered-email') || '';
    if (rememberedEmail) {
      setEmail(rememberedEmail);
    }
  }, []);

  const validateEmail = (value: string): boolean => {
    // Basic email validation
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(value.trim());
  };

  const getLoginFeedback = (message: string): { title: string; hint: string; tone: 'danger' | 'warning' } => {
    if (!message) {
      return { title: '', hint: '', tone: 'danger' };
    }

    if (message.includes('غير صحيحة') || message.includes('غير صحيح') || message.includes('غير موجود')) {
      return {
        title: 'تعذر تسجيل الدخول',
        hint: 'تأكد من البريد الإلكتروني وكلمة المرور، مع الانتباه إلى حالة الأحرف.',
        tone: 'danger'
      };
    }

    if (message.includes('الخادم') || message.includes('الاتصال') || message.includes('الشبكة') || message.includes('مهلة')) {
      return {
        title: 'مشكلة في الاتصال بالخادم',
        hint: 'الخادم أو رابط الـ API غير متاح حاليًا. حاول مرة أخرى بعد لحظات.',
        tone: 'warning'
      };
    }

    if (message.includes('مطلوبة') || message.includes('مطلوبان') || message.includes('صيغة')) {
      return {
        title: 'راجِع بيانات الدخول',
        hint: message,
        tone: 'warning'
      };
    }

    return {
      title: 'حدث خطأ أثناء تسجيل الدخول',
      hint: message,
      tone: 'danger'
    };
  };

  const handlePasswordKeyPress = (event: React.KeyboardEvent<HTMLInputElement>) => {
    // Detect CAPS LOCK
    const capsLockOn = event.getModifierState('CapsLock');
    setCapsLockDetected(capsLockOn);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError('');

    const trimmedEmail = email.trim();
    const rawPassword = password;

    // Validate inputs
    if (!trimmedEmail) {
      setFormError('البريد الإلكتروني مطلوب.');
      return;
    }

    if (!validateEmail(trimmedEmail)) {
      setFormError('البريد الإلكتروني غير صحيح. مثال: president@basmet.local');
      return;
    }

    if (!rawPassword) {
      setFormError('كلمة المرور مطلوبة.');
      return;
    }

    try {
      if (rememberEmail) {
        localStorage.setItem('basma-remembered-email', trimmedEmail.toLowerCase());
      } else {
        localStorage.removeItem('basma-remembered-email');
      }

      await loginUser(trimmedEmail, rawPassword);
    } catch {
      // Errors are handled in AppContext
    }
  };

  const warningMessages = [];
  if (capsLockDetected) {
    warningMessages.push('Caps Lock مفعّل، انتبه إلى حالة الأحرف.');
  }

  return (
    <main className="grid min-h-screen lg:grid-cols-[1.25fr_0.75fr]">
      <section className="relative overflow-hidden px-6 py-10 sm:px-10 lg:px-14 lg:py-14">
        <div className="absolute inset-0 bg-hero-radial" />
        <div className="relative z-10 mx-auto flex h-full max-w-4xl flex-col justify-between gap-8">
          <div>
            <div className="flex items-center gap-4">
              <div className="relative">
                <span className="absolute inset-1 rounded-3xl bg-white/35 blur-xl" aria-hidden="true" />
                <img src="/logo.png" alt="منصة بصمة شباب" className="relative h-16 w-16 rounded-3xl border border-white/20 bg-white/15 object-contain p-2 shadow-glow" />
              </div>
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.45em] text-brand-300/75">منصة بصمة شباب</p>
                <p className="mt-2 text-sm text-slate-300">تنظيم أعمال المؤسسة والمحافظات ومتابعة الأخبار</p>
              </div>
            </div>
            <h1 className="mt-4 max-w-3xl text-3xl font-black leading-tight text-white sm:text-5xl lg:text-6xl">
              منصة بصمة شباب لتنظيم أعمال المؤسسة والمحافظات ومتابعة الأخبار.
            </h1>
          </div>

          <div className="rounded-[2rem] border border-white/15 bg-white/[0.06] p-5 backdrop-blur-sm">
            <p className="text-sm font-bold text-white">لوحة واحدة لكل الفروع</p>
            <p className="mt-2 text-sm leading-7 text-slate-300">
              متابعة المهام والأخبار وحالة المحافظات في عرض واضح، مع صلاحيات دقيقة حسب الدور.
            </p>
            <div className="mt-4 flex flex-wrap gap-2 text-xs">
              <span className="rounded-full border border-white/20 bg-white/10 px-3 py-1 text-slate-100">متابعة المحافظات</span>
              <span className="rounded-full border border-white/20 bg-white/10 px-3 py-1 text-slate-100">إدارة اللجان</span>
              <span className="rounded-full border border-white/20 bg-white/10 px-3 py-1 text-slate-100">آخر الأخبار</span>
            </div>
          </div>
        </div>
      </section>

      <section className="flex items-center justify-center px-6 py-10 sm:px-10 lg:px-12">
        <div className="w-full max-w-xl space-y-6">
        <Card title={loginTitle()} subtitle="Authentication" className="w-full">
          <form className="space-y-4" onSubmit={submit}>
            <Field label="البريد الإلكتروني" hint="مثال: president@basmet.local">
              <Input
                value={email}
                onChange={(event) => {
                  setEmail(event.target.value);
                  if (formError) setFormError('');
                  if (error) clearError();
                }}
                type="email"
                placeholder="president@basmet.local"
                disabled={loading}
                autoComplete="email"
              />
            </Field>

            <Field label="كلمة المرور" hint="حساسة لحالة الأحرف">
              <Input
                value={password}
                onChange={(event) => {
                  setPassword(event.target.value);
                  if (formError) setFormError('');
                  if (error) clearError();
                }}
                onKeyDown={handlePasswordKeyPress}
                type={showPassword ? 'text' : 'password'}
                placeholder="••••••••"
                disabled={loading}
                autoComplete="current-password"
              />
            </Field>

            <div className="flex flex-wrap items-center justify-between gap-3 text-sm text-slate-300">
              <label className="inline-flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={rememberEmail}
                  onChange={(event) => setRememberEmail(event.target.checked)}
                  disabled={loading}
                />
                <span>تذكّر البريد الإلكتروني</span>
              </label>
              <button
                type="button"
                className="rounded-xl border border-white/10 px-3 py-2 text-sm text-slate-200 transition hover:bg-white/5"
                onClick={() => setShowPassword((current) => !current)}
                disabled={loading}
              >
                {showPassword ? 'إخفاء كلمة المرور' : 'إظهار كلمة المرور'}
              </button>
            </div>

            {warningMessages.length > 0 && (
              <div className="rounded-2xl border border-amber-400/20 bg-amber-400/10 px-4 py-3 text-sm text-amber-200">
                {warningMessages.join(' · ')}
              </div>
            )}

            {(formError || error) && (() => {
              const { title, hint, tone } = getLoginFeedback(formError || error);
              return (
                <div className={`rounded-2xl px-4 py-3 space-y-2 ${tone === 'warning' ? 'border border-amber-400/20 bg-amber-400/10' : 'border border-rose-400/20 bg-rose-400/10'}`}>
                  <div className={`text-sm font-semibold ${tone === 'warning' ? 'text-amber-200' : 'text-rose-200'}`}>{title}</div>
                  <div className={`text-xs ${tone === 'warning' ? 'text-amber-100/80' : 'text-rose-100/80'}`}>{hint}</div>
                </div>
              );
            })()}

            <Button type="submit" className="w-full" disabled={loading}>
              {loading ? (
                <span className="inline-flex items-center gap-2">
                  <span className="inline-block h-3 w-3 animate-spin rounded-full border-2 border-white border-t-transparent" />
                  جاري تسجيل الدخول...
                </span>
              ) : (
                'دخول'
              )}
            </Button>
          </form>

          <div className="mt-6 space-y-3">
            <Button variant="secondary" className="w-full" onClick={onNavigateToJoin}>
              <span className="inline-flex items-center gap-2">
                <FiUserPlus />
                التقديم على التيم
              </span>
            </Button>

            <div className="rounded-3xl border border-white/10 bg-white/5 p-4 text-sm leading-7 text-slate-300">
              <p className="font-semibold text-white mb-2">ملاحظات مهمة:</p>
              <ul className="list-inside space-y-1">
                <li>• التسجيل الخارجي مغلق - الحسابات تُنشأ من داخل النظام فقط</li>
                <li>• البريد الإلكتروني يقبل أحرف كبيرة أو صغيرة (مثال: president@basmet.local)</li>
                <li>• إذا نسيت كلمة المرور، تواصل مع المسؤول</li>
              </ul>
            </div>

          </div>
        </Card>

        </div>
      </section>
    </main>
  );
}

function JoinRequestView({ onBackToLogin }: { onBackToLogin: () => void }) {
  const { submitJoinRequest } = useApp();
  const [joinForm, setJoinForm] = useState<TeamJoinRequestCreateState>(emptyJoinRequest);
  const [joinApplicationType, setJoinApplicationType] = useState<'GovernorateMembers' | 'StudentClub'>(emptyJoinRequest.applicationType);
  const [joinGovernorates, setJoinGovernorates] = useState<GovernorateOption[]>([]);
  const [joinCommittees, setJoinCommittees] = useState<CommitteeOption[]>([]);
  const [joinLoading, setJoinLoading] = useState(false);
  const [joinGovernoratesLoading, setJoinGovernoratesLoading] = useState(false);
  const [joinError, setJoinError] = useState('');
  const [joinSuccess, setJoinSuccess] = useState('');

  useEffect(() => {
    let cancelled = false;

    const loadGovernorates = async () => {
      setJoinGovernoratesLoading(true);
      setJoinError('');
      try {
        const data = await getGovernorates();
        if (!cancelled) {
          setJoinGovernorates(data);
          if (data.length === 0) {
            setJoinError('لا توجد محافظات متاحة حاليًا. يرجى التواصل مع الإدارة لإضافة بيانات المحافظات.');
          }
        }
      } catch {
        if (!cancelled) {
          setJoinError('تعذر تحميل المحافظات حاليًا. حاول مرة أخرى بعد قليل.');
        }
      } finally {
        if (!cancelled) {
          setJoinGovernoratesLoading(false);
        }
      }
    };

    void loadGovernorates();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;

    const loadCommittees = async () => {
      if (!joinForm.governorateId) {
        setJoinCommittees([]);
        return;
      }

      try {
        const data = await getGovernorateCommittees(joinForm.governorateId, 'all');
        if (!cancelled) {
          const filteredCommittees = data.filter((committee) =>
            joinApplicationType === 'StudentClub'
              ? committee.isStudentClub
              : !committee.isStudentClub
          );
          setJoinCommittees(filteredCommittees);
        }
      } catch {
        if (!cancelled) {
          setJoinCommittees([]);
        }
      }
    };

    void loadCommittees();

    return () => {
      cancelled = true;
    };
  }, [joinForm.governorateId, joinApplicationType]);

  const submitJoin = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setJoinError('');
    setJoinSuccess('');
    const normalizedNationalId = joinForm.nationalId.replace(/\s+/g, '');

    if (!joinForm.fullName.trim() || !joinForm.email.trim() || !joinForm.phoneNumber.trim() || !joinForm.governorateId || !joinForm.motivation.trim() || !normalizedNationalId) {
      setJoinError('املأ الاسم والبريد والهاتف والرقم القومي والمحافظة وسبب الانضمام قبل الإرسال.');
      return;
    }

    if (!joinForm.committeeId) {
      setJoinError('اختر لجنة قبل الإرسال.');
      return;
    }

    if (normalizedNationalId.length !== 14 || /[^0-9]/.test(normalizedNationalId)) {
      setJoinError('الرقم القومي يجب أن يكون 14 رقمًا.');
      return;
    }

    setJoinLoading(true);
    try {
      const created = await submitJoinRequest(joinForm);
      setJoinSuccess(`تم إرسال طلبك إلى منسق محافظة ${created.governorateName}${created.assignedToMemberName ? ` (${created.assignedToMemberName})` : ''}.`);
      setJoinForm(emptyJoinRequest);
      setJoinApplicationType(emptyJoinRequest.applicationType);
      setJoinCommittees([]);
    } catch (joinSubmitError) {
      setJoinError(joinSubmitError instanceof Error ? joinSubmitError.message : 'تعذر إرسال الطلب حاليًا.');
    } finally {
      setJoinLoading(false);
    }
  };

  return (
    <main className="grid min-h-screen place-items-center px-6 py-10 sm:px-10 lg:px-12">
      <div className="w-full max-w-3xl space-y-5">
        <Button variant="ghost" onClick={onBackToLogin}>
          <span className="inline-flex items-center gap-2">
            <FiArrowLeft />
            العودة لتسجيل الدخول
          </span>
        </Button>

        <Card title="التقديم على التيم" subtitle="Join request">
          <form className="space-y-4" onSubmit={submitJoin}>
            <Field label="الاسم رباعي">
              <Input value={joinForm.fullName} onChange={(event) => setJoinForm((current) => ({ ...current, fullName: event.target.value }))} placeholder="الاسم الكامل" disabled={joinLoading} required />
            </Field>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="البريد الإلكتروني">
                <Input value={joinForm.email} onChange={(event) => setJoinForm((current) => ({ ...current, email: event.target.value }))} type="email" placeholder="name@example.com" disabled={joinLoading} required />
              </Field>
              <Field label="رقم الهاتف">
                <Input value={joinForm.phoneNumber} onChange={(event) => setJoinForm((current) => ({ ...current, phoneNumber: event.target.value }))} inputMode="tel" placeholder="01xxxxxxxxx" disabled={joinLoading} required />
              </Field>
            </div>
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="المحافظة">
                <Select
                  value={joinForm.governorateId}
                  onChange={(event) => setJoinForm((current) => ({ ...current, governorateId: event.target.value, committeeId: '' }))}
                  disabled={joinLoading || joinGovernoratesLoading}
                  required
                >
                  <option value="">{joinGovernoratesLoading ? 'جارٍ تحميل المحافظات...' : 'اختر المحافظة'}</option>
                  {joinGovernorates.map((governorate) => <option key={governorate.governorateId} value={governorate.governorateId}>{governorate.name}</option>)}
                </Select>
              </Field>
              <Field label="نوع التقديم">
                <Select
                  value={joinApplicationType}
                  onChange={(event) => {
                    const applicationType = event.target.value as 'GovernorateMembers' | 'StudentClub';
                    setJoinApplicationType(applicationType);
                    setJoinForm((current) => ({ ...current, applicationType, committeeId: '' }));
                  }}
                  disabled={joinLoading || !joinForm.governorateId}
                >
                  <option value="StudentClub">نادي طلابي</option>
                  <option value="GovernorateMembers">أعضاء محافظة</option>
                </Select>
              </Field>
              <Field label={joinApplicationType === 'StudentClub' ? 'النادي الطلابي' : 'لجنة المحافظة'}>
                <Select
                  value={joinForm.committeeId}
                  onChange={(event) => setJoinForm((current) => ({ ...current, committeeId: event.target.value }))}
                  disabled={joinLoading || !joinForm.governorateId}
                  required
                >
                  <option value="">{joinApplicationType === 'StudentClub' ? 'اختر ناديًا طلابيًا' : 'اختر لجنة محافظة'}</option>
                  {joinCommittees.map((committee) => <option key={committee.committeeId} value={committee.committeeId}>{committee.name}</option>)}
                </Select>
              </Field>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="الرقم القومي" hint="إجباري - 14 رقمًا">
                <Input
                  value={joinForm.nationalId}
                  onChange={(event) => {
                    const nationalId = event.target.value.replace(/\D/g, '').slice(0, 14);
                    setJoinForm((current) => ({
                      ...current,
                      nationalId,
                      birthDate: extractBirthDateFromNationalId(nationalId) ?? current.birthDate
                    }));
                  }}
                  inputMode="numeric"
                  required
                  minLength={14}
                  maxLength={14}
                  pattern="[0-9]{14}"
                  placeholder="14 رقمًا"
                  disabled={joinLoading}
                />
              </Field>
              <Field label="تاريخ الميلاد">
                <Input value={joinForm.birthDate} onChange={(event) => setJoinForm((current) => ({ ...current, birthDate: event.target.value }))} type="date" disabled={joinLoading} />
              </Field>
            </div>
            <Field label="سبب الانضمام">
              <Textarea value={joinForm.motivation} onChange={(event) => setJoinForm((current) => ({ ...current, motivation: event.target.value }))} rows={4} placeholder="اكتب لماذا تريد الانضمام وكيف ستفيد الفريق" disabled={joinLoading} required />
            </Field>
            <Field label="الخبرات السابقة">
              <Textarea value={joinForm.experience} onChange={(event) => setJoinForm((current) => ({ ...current, experience: event.target.value }))} rows={3} placeholder="اختياري" disabled={joinLoading} />
            </Field>

            {joinError && <div className="rounded-2xl border border-rose-400/20 bg-rose-400/10 px-4 py-3 text-sm text-rose-200">{joinError}</div>}
            {joinSuccess && <div className="rounded-2xl border border-emerald-400/20 bg-emerald-400/10 px-4 py-3 text-sm text-emerald-200">{joinSuccess}</div>}

            <Button type="submit" className="w-full" disabled={joinLoading}>
              <span className="inline-flex items-center gap-2">
                <FiUserPlus />
                {joinLoading ? 'جارٍ إرسال الطلب...' : 'إرسال طلب الالتحاق'}
              </span>
            </Button>
          </form>
        </Card>
      </div>
    </main>
  );
}

function PasswordChangeView() {
  const { user, changePassword, loading, error, clearError } = useApp();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [formError, setFormError] = useState('');

  useEffect(() => {
    clearError();
  }, [clearError]);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError('');

    if (newPassword.length < 8) {
      setFormError('كلمة المرور الجديدة يجب أن تكون 8 أحرف على الأقل.');
      return;
    }

    if (newPassword !== confirmPassword) {
      setFormError('كلمة المرور الجديدة وتأكيدها غير متطابقتين.');
      return;
    }

    await changePassword(currentPassword, newPassword);
  };

  const message = formError || error;
  const subtitle = user?.mustChangePassword ? 'Mandatory security step' : 'تحديث كلمة المرور';
  const note = user?.mustChangePassword
    ? 'الحساب الحالي يستخدم كلمة مرور مؤقتة.'
    : 'يمكنك تحديث كلمة المرور في أي وقت من هنا.';

  return (
    <Card title="تغيير كلمة المرور" subtitle={subtitle} className="w-full">
      <form className="space-y-4" onSubmit={submit}>
        <Field label="كلمة المرور الحالية">
          <Input value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} type="password" placeholder="••••••••" />
        </Field>
        <Field label="كلمة المرور الجديدة">
          <Input value={newPassword} onChange={(event) => setNewPassword(event.target.value)} type="password" placeholder="ثمانية أحرف على الأقل" />
        </Field>
        <Field label="تأكيد كلمة المرور الجديدة">
          <Input value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} type="password" placeholder="أعد كتابة كلمة المرور الجديدة" />
        </Field>

        {message && <div className="rounded-2xl border border-rose-400/20 bg-rose-400/10 px-4 py-3 text-sm text-rose-200">{message}</div>}

        <Button type="submit" className="w-full" disabled={loading}>
          {loading ? 'جاري تغيير كلمة المرور...' : 'تغيير كلمة المرور'}
        </Button>
      </form>

      <div className="mt-6 rounded-3xl border border-white/10 bg-white/5 p-4 text-sm leading-7 text-slate-300">
        {note} {user?.fullName ? `الحساب الحالي لـ ${user.fullName}.` : ''}
      </div>
    </Card>
  );
}

function OverviewPage() {
  const { dashboard, leaderboard, activityLogs, members, tasks, myComplaints, news } = useApp();

  const cards = [
    { label: 'إجمالي الأعضاء', value: dashboard?.totalMembers ?? members.length, hint: 'كل الأعضاء في النظام', accent: 'brand' as const },
    { label: 'الرسائل المفتوحة', value: dashboard?.openComplaints ?? myComplaints.length, hint: 'الشكاوى غير المحسومة', accent: 'rose' as const },
    { label: 'إجمالي المهام', value: tasks.length, hint: 'المهام الحالية', accent: 'sky' as const },
    { label: 'أفضل رصيد نقاط', value: leaderboard[0]?.points ?? dashboard?.points ?? 0, hint: 'المتصدر الحالي', accent: 'amber' as const }
  ];

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.overview.eyebrow}
        title={pageTitles.overview.title}
        description={pageTitles.overview.description}
      />

      <div className="grid gap-4 xl:grid-cols-4 md:grid-cols-2">
        {cards.map((card) => <StatCard key={card.label} {...card} />)}
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
        <Card title="أفضل المتصدرين" subtitle="Top 10">
          <div className="space-y-3">
            {leaderboard.length === 0 ? (
              <EmptyState title="لا توجد بيانات بعد" description="ستظهر قائمة المتصدرين هنا عند توفر النقاط." />
            ) : leaderboard.map((entry) => (
              <div key={entry.memberId} className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-white/10 bg-white/5 px-4 py-3">
                <div>
                  <p className="font-bold text-white">#{entry.rank} {entry.fullName}</p>
                  <p className="text-sm text-slate-400">{roleLabel(entry.role)}</p>
                </div>
                <Badge tone="success">{entry.points} نقطة</Badge>
              </div>
            ))}
          </div>
        </Card>

        <Card title="النشاط الأخير" subtitle="Activity log">
          <div className="space-y-3">
            {activityLogs.length === 0 ? (
              <EmptyState title="لا يوجد نشاط بعد" description="سجل الأعمال سيظهر هنا بعد تنفيذ أي عملية داخل النظام." />
            ) : activityLogs.map((item) => (
              <div key={item.id} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="font-bold text-white">{item.title}</p>
                  <Badge tone={item.tone === 'warning' ? 'warning' : item.tone === 'success' ? 'success' : 'neutral'}>{formatDate(item.createdAtUtc)}</Badge>
                </div>
                <p className="mt-2 text-sm leading-7 text-slate-300">{item.description}</p>
              </div>
            ))}
          </div>
        </Card>
      </div>

      <Card title="آخر الأخبار" subtitle="News feed">
        {news.length === 0 ? (
          <EmptyState title="لا توجد أخبار" description="عند نشر خبر جديد من الإدارة سيظهر هنا مباشرة." />
        ) : (
          <div className="space-y-3">
            {news.slice(0, 4).map((item) => (
              <div key={item.id} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="font-bold text-white">{item.title}</p>
                  <Badge tone="brand">{audienceLabel(item.audienceType)}</Badge>
                </div>
                <p className="mt-2 text-sm leading-7 text-slate-300">{item.content}</p>
                <p className="mt-2 text-xs text-slate-400">{item.createdByName} · {formatDate(item.createdAtUtc)}</p>
              </div>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}

function LeaderboardPage() {
  const { dashboard, leaderboard } = useApp();
  const topTen = leaderboard.slice(0, 10);

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.leaderboard.eyebrow}
        title={pageTitles.leaderboard.title}
        description={pageTitles.leaderboard.description}
      />

      <div className="grid gap-4 md:grid-cols-3">
        <StatCard label="إجمالي الأعضاء" value={dashboard?.totalMembers ?? 0} accent="brand" />
        <StatCard label="المتصدرون المعروضون" value={topTen.length} accent="success" />
        <StatCard label="أعلى نقاط" value={topTen[0]?.points ?? 0} accent="amber" />
      </div>

      <Card title="أفضل 10 أعضاء" subtitle="Top 10 overall">
        {topTen.length === 0 ? (
          <EmptyState title="لا يوجد بيانات" description="سيظهر المتصدرون هنا بمجرد وجود نقاط للأعضاء." />
        ) : (
          <div className="space-y-3">
            {topTen.map((entry) => (
              <div key={entry.memberId} className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-white/10 bg-slate-950/45 px-4 py-3">
                <div>
                  <p className="font-bold text-white">#{entry.rank} {entry.fullName}</p>
                  <p className="text-sm text-slate-400">{roleLabel(entry.role)}</p>
                </div>
                <Badge tone="success">{entry.points} نقطة</Badge>
              </div>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}

function NewsPage() {
  const { news, members, canManageNews, createNewsItem, deleteNewsItem, loading } = useApp();
  const [createOpen, setCreateOpen] = useState(false);
  const [newsForm, setNewsForm] = useState<NewsCreateState>(emptyNews);
  const isLoading = loading && news.length === 0;

  const removeNews = async (item: NewsItem) => {
    if (!canManageNews) return;
    if (!window.confirm(`هل تريد حذف الخبر "${item.title}"؟`)) {
      return;
    }

    await deleteNewsItem(item.id);
  };

  const toggleRole = (role: Role) => {
    setNewsForm((current) => ({
      ...current,
      targetRoles: current.targetRoles.includes(role)
        ? current.targetRoles.filter((item) => item !== role)
        : [...current.targetRoles, role]
    }));
  };

  const toggleMember = (memberId: string) => {
    setNewsForm((current) => ({
      ...current,
      targetMemberIds: current.targetMemberIds.includes(memberId)
        ? current.targetMemberIds.filter((item) => item !== memberId)
        : [...current.targetMemberIds, memberId]
    }));
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await createNewsItem(newsForm);
    setNewsForm(emptyNews);
    setCreateOpen(false);
  };

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.news.eyebrow}
        title={pageTitles.news.title}
        description={pageTitles.news.description}
        actions={canManageNews ? <Button onClick={() => setCreateOpen(true)}><span className="inline-flex items-center gap-2"><FiPlus /> خبر جديد</span></Button> : undefined}
      />

      <Card title="قائمة الأخبار" subtitle="Visible news">
        {isLoading ? (
          <EmptyState title="جاري تحميل الأخبار" description="يتم الآن جلب الأخبار من الخادم." />
        ) : news.length === 0 ? (
          <EmptyState title="لا يوجد أخبار حالياً" description="عند نشر إعلان جديد من الإدارة سيظهر هنا." />
        ) : (
          <div className="space-y-3">
            {news.map((item: NewsItem) => (
              <div key={item.id} className="rounded-3xl border border-white/10 bg-white/5 p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="text-lg font-bold text-white">{item.title}</p>
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge tone="brand">{audienceLabel(item.audienceType)}</Badge>
                    {canManageNews && (
                      <Button variant="ghost" className="px-3 py-2" onClick={() => void removeNews(item)}>
                        <span className="inline-flex items-center gap-2"><FiTrash2 /> حذف</span>
                      </Button>
                    )}
                  </div>
                </div>
                <p className="mt-3 text-sm leading-7 text-slate-300">{item.content}</p>
                <div className="mt-3 text-xs text-slate-400">
                  <p>بواسطة: {item.createdByName}</p>
                  <p>الوقت: {formatDate(item.createdAtUtc)}</p>
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      <Modal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        title="نشر خبر جديد"
        subtitle="News publisher"
        footer={<><Button variant="ghost" onClick={() => setCreateOpen(false)}>إلغاء</Button><Button type="submit" form="news-form"><span className="inline-flex items-center gap-2"><FiSave /> نشر</span></Button></>}
      >
        <form id="news-form" className="space-y-4" onSubmit={(event) => void submit(event)}>
          <Field label="العنوان">
            <Input value={newsForm.title} onChange={(event) => setNewsForm((current) => ({ ...current, title: event.target.value }))} />
          </Field>
          <Field label="المحتوى">
            <Textarea value={newsForm.content} onChange={(event) => setNewsForm((current) => ({ ...current, content: event.target.value }))} rows={5} />
          </Field>
          <Field label="الجمهور">
            <Select value={newsForm.audienceType} onChange={(event) => setNewsForm((current) => ({ ...current, audienceType: event.target.value as NewsCreateState['audienceType'], targetRoles: [], targetMemberIds: [] }))}>
              <option value="All">للجميع</option>
              <option value="Roles">أدوار محددة</option>
              <option value="Members">أعضاء محددين</option>
            </Select>
          </Field>

          {newsForm.audienceType === 'Roles' && (
            <Field label="اختر الأدوار">
              <div className="grid gap-2 sm:grid-cols-2">
                {(['President', 'VicePresident', 'CentralMember', 'GovernorCoordinator', 'GovernorCommitteeCoordinator', 'CommitteeMember'] as Role[]).map((role) => (
                  <label key={role} className="flex items-center gap-2 rounded-xl border border-white/10 bg-white/5 px-3 py-2 text-sm text-slate-200">
                    <input type="checkbox" checked={newsForm.targetRoles.includes(role)} onChange={() => toggleRole(role)} />
                    <span>{roleLabel(role)}</span>
                  </label>
                ))}
              </div>
            </Field>
          )}

          {newsForm.audienceType === 'Members' && (
            <Field label="اختر الأعضاء">
              <div className="max-h-56 space-y-2 overflow-auto rounded-2xl border border-white/10 bg-white/5 p-3">
                {members.map((member) => (
                  <label key={member.memberId} className="flex items-center gap-2 text-sm text-slate-200">
                    <input type="checkbox" checked={newsForm.targetMemberIds.includes(member.memberId)} onChange={() => toggleMember(member.memberId)} />
                    <span>{member.fullName}</span>
                  </label>
                ))}
              </div>
            </Field>
          )}
        </form>
      </Modal>
    </div>
  );
}

function JoinRequestsPage() {
  const { joinRequests, search, canReviewJoinRequests, reviewJoinRequestItem, loading } = useApp();
  const [joinRequestNotes, setJoinRequestNotes] = useState<Record<string, string>>({});
  const [governorateFilter, setGovernorateFilter] = useState('');

  const isLoading = loading && joinRequests.length === 0;

  const governorateOptions = useMemo(() => {
    return Array.from(new Set(joinRequests.map((item) => item.governorateName).filter(Boolean)))
      .sort((left, right) => left.localeCompare(right, 'ar'));
  }, [joinRequests]);

  const filteredJoinRequests = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    return joinRequests.filter((item) => {
      if (governorateFilter && item.governorateName !== governorateFilter) {
        return false;
      }

      return [
        item.fullName,
        item.email,
        item.phoneNumber,
        item.governorateName,
        item.committeeName ?? '',
        item.status,
        item.assignedToMemberName ?? ''
      ].join(' ').toLowerCase().includes(normalized);
    });
  }, [joinRequests, search, governorateFilter]);

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.joinrequests.eyebrow}
        title={pageTitles.joinrequests.title}
        description={pageTitles.joinrequests.description}
      />

      {!canReviewJoinRequests ? (
        <Card title="طلبات الالتحاق" subtitle="Join requests">
          <EmptyState title="لا توجد صلاحية" description="هذا القسم يظهر لمن لديهم صلاحية مراجعة طلبات الالتحاق فقط." />
        </Card>
      ) : (
        <Card title="طلبات الالتحاق" subtitle="Join requests routed by governorate">
          <div className="mb-4 grid gap-3 md:grid-cols-2">
            <Field label="المحافظة">
              <Select value={governorateFilter} onChange={(event) => setGovernorateFilter(event.target.value)}>
                <option value="">الكل</option>
                {governorateOptions.map((name) => (
                  <option key={name} value={name}>{name}</option>
                ))}
              </Select>
            </Field>
          </div>
          {isLoading ? (
            <EmptyState title="جاري تحميل الطلبات" description="يتم الآن جلب طلبات الالتحاق الخاصة بمحافظتك." />
          ) : filteredJoinRequests.length === 0 ? (
            <EmptyState title="لا توجد طلبات حالياً" description="عند تقديم طلب جديد سيظهر هنا للمتابعة والمراجعة." />
          ) : (
            <div className="space-y-4">
              {filteredJoinRequests.map((item) => (
                <div key={item.id} className="rounded-3xl border border-white/10 bg-white/5 p-4">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <p className="text-lg font-bold text-white">{item.fullName}</p>
                      <p className="text-sm text-slate-400">{item.email} · {item.phoneNumber}</p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Badge tone={item.status === 'Accepted' ? 'success' : item.status === 'Rejected' ? 'danger' : 'warning'}>
                        {joinRequestStatusLabels[item.status] ?? item.status}
                      </Badge>
                      <Badge tone="brand">{item.governorateName}</Badge>
                    </div>
                  </div>

                  <div className="mt-4 grid gap-3 md:grid-cols-2">
                    <div className="rounded-2xl border border-white/10 bg-slate-950/40 p-3 text-sm text-slate-300">
                      <p>اللجنة: {item.committeeName ?? 'غير محددة'}</p>
                      <p className="mt-1">المحافظة: {item.governorateName}</p>
                      <p className="mt-1">الرقم القومي: {item.nationalId ?? '—'}</p>
                      <p className="mt-1">تاريخ الميلاد: {item.birthDate ? formatDateOnly(item.birthDate) : '—'}</p>
                      <p className="mt-1">تاريخ الإرسال: {formatDate(item.createdAtUtc)}</p>
                    </div>
                    <div className="rounded-2xl border border-white/10 bg-slate-950/40 p-3 text-sm text-slate-300">
                      <p className="font-semibold text-white">سبب الانضمام</p>
                      <p className="mt-2 leading-7">{item.motivation}</p>
                    </div>
                  </div>

                  {item.experience && (
                    <div className="mt-3 rounded-2xl border border-white/10 bg-slate-950/30 p-3 text-sm text-slate-300">
                      <p className="font-semibold text-white">الخبرات السابقة</p>
                      <p className="mt-2 leading-7">{item.experience}</p>
                    </div>
                  )}

                  <div className="mt-4 grid gap-3 lg:grid-cols-[1fr_auto]">
                    <Textarea
                      value={joinRequestNotes[item.id] ?? item.adminNotes ?? ''}
                      onChange={(event) => setJoinRequestNotes((current) => ({ ...current, [item.id]: event.target.value }))}
                      rows={3}
                      placeholder="ملاحظات المنسق أو الإدارة"
                    />
                    <div className="flex flex-wrap items-center gap-2">
                      <Button variant="secondary" onClick={() => void reviewJoinRequestItem(item.id, { status: 'Accepted', adminNotes: joinRequestNotes[item.id] ?? item.adminNotes ?? '' })}>
                        قبول
                      </Button>
                      <Button variant="danger" onClick={() => void reviewJoinRequestItem(item.id, { status: 'Rejected', adminNotes: joinRequestNotes[item.id] ?? item.adminNotes ?? '' })}>
                        رفض
                      </Button>
                    </div>
                  </div>

                  {(item.reviewedByMemberName || item.reviewedAtUtc) && (
                    <p className="mt-3 text-xs text-slate-400">
                      تمت المراجعة بواسطة {item.reviewedByMemberName ?? 'الإدارة'} {item.reviewedAtUtc ? `في ${formatDate(item.reviewedAtUtc)}` : ''}
                    </p>
                  )}
                </div>
              ))}
            </div>
          )}
        </Card>
      )}
    </div>
  );
}

function MembersPage() {
  const { members, createMember, deleteMember, changeRole, assignPermission, changePoints, resetPassword, canManageUsers, canCreateMembers, user } = useApp();
  const [createOpen, setCreateOpen] = useState(false);
  const [page, setPage] = useState(1);
  const pageSize = 6;
  const [memberSearch, setMemberSearch] = useState('');
  const [selectedMemberId, setSelectedMemberId] = useState('');
  const [selectedRole, setSelectedRole] = useState<Role>('CommitteeMember');
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>(['Users.Manage']);
  const [pointForm, setPointForm] = useState<PointFormState>(emptyPoint);
  const [memberForm, setMemberForm] = useState<MemberCreateFormState>(emptyMember);
  const [governorates, setGovernorates] = useState<GovernorateOption[]>([]);
  const [committees, setCommittees] = useState<CommitteeOption[]>([]);
  const [scopeLoading, setScopeLoading] = useState(false);
  const [memberFormError, setMemberFormError] = useState('');
  const visibleGovernorates = useMemo(() => {
    if (user?.role === 'GovernorCoordinator' && user.governorName) {
      return governorates.filter((governorate) => governorate.name === user.governorName);
    }

    return governorates;
  }, [governorates, user?.governorName, user?.role]);

  const filtered = useMemo(() => {
    const normalized = memberSearch.trim().toLowerCase();
    if (!normalized) {
      return members;
    }

    return members.filter((member) => [member.fullName, member.email].join(' ').toLowerCase().includes(normalized));
  }, [members, memberSearch]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const memberPageRows = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, page, pageSize]);

  useEffect(() => {
    setPage(1);
  }, [memberSearch]);

  useEffect(() => {
    if (!selectedMemberId && filtered[0]) {
      setSelectedMemberId(filtered[0].memberId);
    }
  }, [filtered, selectedMemberId]);

  useEffect(() => {
    setSelectedPermissions([]);
  }, [selectedMemberId]);

  useEffect(() => {
    if (!createOpen) {
      return;
    }

    let cancelled = false;
    const loadGovernorates = async () => {
      setScopeLoading(true);
      try {
        const result = await getGovernorates();
        if (!cancelled) {
          setGovernorates(result);
        }
      } catch {
        if (!cancelled) {
          setGovernorates([]);
        }
      } finally {
        if (!cancelled) {
          setScopeLoading(false);
        }
      }
    };

    void loadGovernorates();

    return () => {
      cancelled = true;
    };
  }, [createOpen]);

  useEffect(() => {
    if (!roleNeedsGovernorate(memberForm.role) || !memberForm.governorateId) {
      setCommittees([]);
      return;
    }

    let cancelled = false;
    const loadCommittees = async () => {
      setScopeLoading(true);
      try {
        const result = await getGovernorateCommittees(memberForm.governorateId);
        if (!cancelled) {
          setCommittees(result);
        }
      } catch {
        if (!cancelled) {
          setCommittees([]);
        }
      } finally {
        if (!cancelled) {
          setScopeLoading(false);
        }
      }
    };

    void loadCommittees();

    return () => {
      cancelled = true;
    };
  }, [memberForm.governorateId, memberForm.role]);

  useEffect(() => {
    if (!roleNeedsGovernorate(memberForm.role)) {
      setMemberForm((current) => ({ ...current, governorateId: '', committeeId: '' }));
      setCommittees([]);
      return;
    }

    if (!roleNeedsCommittee(memberForm.role)) {
      setMemberForm((current) => ({ ...current, committeeId: '' }));
    }
  }, [memberForm.role]);

  const selectedMember = filtered.find((item) => item.memberId === selectedMemberId) ?? filtered[0] ?? null;
  const canManageSelectedMember = useMemo(() => {
    if (!user || !selectedMember) {
      return false;
    }

    if (user.role === 'President' || user.role === 'VicePresident') {
      return true;
    }

    if (user.id === selectedMember.memberId) {
      return true;
    }

    const sameGovernorate = isSameScopeName(user.governorName, selectedMember.governorName);
    const sameCommittee = isSameScopeName(user.committeeName, selectedMember.committeeName);

    if (user.role === 'GovernorCoordinator') {
      return sameGovernorate && selectedMember.role !== 'President' && selectedMember.role !== 'VicePresident';
    }

    if (user.role === 'GovernorCommitteeCoordinator') {
      return sameGovernorate && sameCommittee && selectedMember.role === 'CommitteeMember';
    }

    if (user.role === 'CentralMember') {
      return selectedMember.role === 'CommitteeMember';
    }

    return false;
  }, [selectedMember, user]);
  const canGrantSelectedPermissions = canManageUsers || canManageSelectedMember;
  const canDeleteSelectedMember = Boolean(selectedMember)
    && (canManageUsers || canManageSelectedMember)
    && selectedMember?.memberId !== user?.id
    && (selectedMember?.role !== 'President' || user?.role === 'President')
    && (selectedMember?.role !== 'VicePresident' || user?.role === 'President');

  const columns: TableColumn<MemberAdminItem>[] = [
    { header: 'الاسم', render: (row) => <div><p className="font-bold text-white">{row.fullName}</p><p className="text-xs text-slate-400">{row.email}</p></div> },
    { header: 'الهوية', render: (row) => <div className="text-xs text-slate-300"><p>{row.nationalId ?? '—'}</p><p>{row.birthDate ? formatDateOnly(row.birthDate) : '—'}</p></div> },
    { header: 'الدور', render: (row) => <Badge tone="brand">{roleLabel(row.role)}</Badge> },
    { header: 'النطاق', render: (row) => <div className="text-xs text-slate-300">{row.governorName ? <p>المحافظة: {row.governorName}</p> : <p>بدون محافظة</p>}{row.committeeName ? <p>اللجنة: {row.committeeName}</p> : <p>بدون لجنة</p>}</div> },
    { header: 'النقاط', render: (row) => <Badge tone="success">{row.points}</Badge> },
    { header: 'الصلاحيات', render: (row) => row.permissions.length },
    { header: 'الإجراءات', render: (row) => <Button variant="ghost" onClick={() => setSelectedMemberId(row.memberId)}>اختيار</Button> }
  ];

  const saveMember = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setMemberFormError('');

    const nameParts = memberForm.fullName.trim().split(/\s+/).filter(Boolean);
    const normalizedNationalId = memberForm.nationalId.replace(/\s+/g, '');

    if (!memberForm.fullName.trim()) {
      setMemberFormError('الاسم الكامل مطلوب.');
      return;
    }

    if (nameParts.length < 4) {
      setMemberFormError(`الاسم يجب أن يكون رباعياً (${nameParts.length} أجزاء فقط). الرجاء إدخال الاسم الكامل.`);
      return;
    }

    if (!memberForm.email.trim()) {
      setMemberFormError('البريد الإلكتروني مطلوب.');
      return;
    }

    if (normalizedNationalId.length === 0) {
      setMemberFormError('الرقم القومي مطلوب.');
      return;
    }

    if (normalizedNationalId.length !== 14) {
      setMemberFormError(`الرقم القومي يجب أن يكون 14 رقماً (أدخل الآن ${normalizedNationalId.length}).`);
      return;
    }

    if (/[^0-9]/.test(normalizedNationalId)) {
      setMemberFormError('الرقم القومي يجب أن يحتوي على أرقام فقط، بلا أحرف أو رموز.');
      return;
    }

    if (!memberForm.birthDate) {
      const extractedFromId = extractBirthDateFromNationalId(normalizedNationalId);
      if (!extractedFromId) {
        setMemberFormError('تاريخ الميلاد مطلوب. لم نتمكن من استخراجه من الرقم القومي.');
        return;
      }
      // Auto-fill birthDate and retry
      setMemberForm((current) => ({ ...current, birthDate: extractedFromId }));
      return;
    }

    if (roleNeedsGovernorate(memberForm.role) && !memberForm.governorateId) {
      setMemberFormError('المحافظة مطلوبة لهذا الدور.');
      return;
    }

    if (roleNeedsCommittee(memberForm.role) && !memberForm.committeeId) {
      setMemberFormError('اللجنة مطلوبة لهذا الدور.');
      return;
    }

    try {
      await createMember(memberForm);
      setCreateOpen(false);
      setMemberForm(emptyMember);
      setCommittees([]);
      setMemberFormError('');
    } catch (error) {
      setMemberFormError(error instanceof Error ? error.message : 'فشل إنشاء العضو. تحقق من البيانات وحاول مرة أخرى.');
    }
  };

  const saveRole = async () => {
    if (!selectedMember) return;
    await changeRole(selectedMember.memberId, selectedRole);
  };

  const savePermission = async () => {
    if (!selectedMember || selectedPermissions.length === 0 || !canGrantSelectedPermissions) return;

    for (const permission of selectedPermissions) {
      await assignPermission(selectedMember.memberId, permission);
    }

    setSelectedPermissions([]);
  };

  const savePoints = async () => {
    if (!selectedMember || !pointForm.reason.trim()) return;
    await changePoints(selectedMember.memberId, pointForm);
    setPointForm(emptyPoint);
  };

  const resetMemberPassword = async () => {
    if (!selectedMember) return;
    if (window.confirm(`هل تريد فعلاً إعادة تعيين كلمة المرور للعضو "${selectedMember.fullName}" إلى Test123.؟`)) {
      await resetPassword(selectedMember.memberId);
    }
  };

  const removeMember = async () => {
    if (!selectedMember || !canDeleteSelectedMember) return;
    if (!window.confirm(`هل تريد حذف العضو "${selectedMember.fullName}" نهائياً؟`)) {
      return;
    }

    await deleteMember(selectedMember.memberId);
    setSelectedMemberId('');
  };

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.members.eyebrow}
        title={pageTitles.members.title}
        description={pageTitles.members.description}
        actions={canCreateMembers ? <Button onClick={() => setCreateOpen(true)}><span className="inline-flex items-center gap-2"><FiPlus /> عضو جديد</span></Button> : undefined}
      />

      <div className="grid gap-6 xl:grid-cols-[1.3fr_0.7fr]">
        <Card title="قائمة الأعضاء" subtitle="Members">
          <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="max-w-md flex-1">
              <Input
                value={memberSearch}
                onChange={(event) => setMemberSearch(event.target.value)}
                placeholder="ابحث بالاسم أو البريد الإلكتروني"
              />
            </div>
            <span className="text-sm text-slate-400">{filtered.length} عضو</span>
          </div>
          <div className="space-y-3 sm:hidden">
            {memberPageRows.length === 0 ? (
              <EmptyState title="لا يوجد أعضاء" description="لن تظهر العناصر هنا إلا عند توفر بيانات الأعضاء من الـ API." />
            ) : (
              memberPageRows.map((member) => (
                <div
                  key={member.memberId}
                  className={`rounded-2xl border p-4 ${member.memberId === selectedMemberId ? 'border-brand-400/40 bg-white/10' : 'border-white/10 bg-white/5'}`}
                >
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div>
                      <p className="font-bold text-white">{member.fullName}</p>
                      <p className="mt-1 break-all text-xs text-slate-400">{member.email}</p>
                    </div>
                    <Badge tone="brand">{roleLabel(member.role)}</Badge>
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    <Badge tone="success">{member.points} نقطة</Badge>
                    <Badge tone="neutral">{member.permissions.length} صلاحية</Badge>
                  </div>
                  <p className="mt-3 text-xs text-slate-400">
                    {member.governorName ? `المحافظة: ${member.governorName}` : 'بدون محافظة'}
                    {' '}
                    ·
                    {' '}
                    {member.committeeName ? `اللجنة: ${member.committeeName}` : 'بدون لجنة'}
                  </p>
                  <Button className="mt-3 w-full" variant="secondary" onClick={() => setSelectedMemberId(member.memberId)}>
                    اختيار العضو
                  </Button>
                </div>
              ))
            )}
            <div className="flex flex-col gap-2 pt-2">
              <p className="text-xs text-slate-400">الصفحة {page} من {totalPages} - {filtered.length} عضو</p>
              <div className="flex gap-2">
                <Button variant="secondary" disabled={page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))}>السابق</Button>
                <Button variant="secondary" disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>التالي</Button>
              </div>
            </div>
          </div>
          <div className="hidden sm:block">
            <PagedTable
              rows={filtered}
              columns={columns}
              rowKey={(row) => row.memberId}
              page={page}
              pageSize={pageSize}
              onPageChange={setPage}
              emptyTitle="لا يوجد أعضاء"
              emptyDescription="لن تظهر العناصر هنا إلا عند توفر بيانات الأعضاء من الـ API."
            />
          </div>
        </Card>

        <Card title="إدارة العضو" subtitle="Selected member">
          {selectedMember ? (
            <div className="space-y-4">
              <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <p className="text-lg font-bold text-white">{selectedMember.fullName}</p>
                <p className="text-sm text-slate-400">{selectedMember.email}</p>
                <div className="mt-3 flex flex-wrap gap-2">
                  <Badge tone="brand">{roleLabel(selectedMember.role)}</Badge>
                  <Badge tone="success">{selectedMember.points} نقطة</Badge>
                </div>
                <p className="mt-3 text-sm text-slate-300">الرقم القومي: {selectedMember.nationalId ?? 'غير مسجل'}</p>
                <p className="text-sm text-slate-300">تاريخ الميلاد: {selectedMember.birthDate ? formatDateOnly(selectedMember.birthDate) : 'غير مسجل'}</p>
                <p className="mt-3 text-sm text-slate-300">
                  {selectedMember.governorName ? `المحافظة: ${selectedMember.governorName}` : 'بدون محافظة'}
                  {' '}
                  ·
                  {' '}
                  {selectedMember.committeeName ? `اللجنة: ${selectedMember.committeeName}` : 'بدون لجنة'}
                </p>
              </div>

              <Field label="تغيير الدور">
                <Select value={selectedRole} onChange={(event) => setSelectedRole(event.target.value as Role)}>
                  {Object.keys(roleLabels).map((role) => <option key={role} value={role}>{roleLabel(role)}</option>)}
                </Select>
              </Field>
              <Button className="w-full" onClick={() => void saveRole()} disabled={!canManageUsers}>حفظ الدور</Button>

              <Field label="منح صلاحيات">
                <div className="max-h-56 overflow-auto rounded-2xl border border-white/10 bg-white/5 p-3">
                  <div className="grid gap-2">
                    {permissionOptionsWithJoinRequests.map((permission) => (
                      <label key={permission.key} className="flex cursor-pointer items-center gap-3 rounded-xl bg-slate-950/45 px-3 py-2 text-sm text-slate-200 transition hover:bg-slate-950/70">
                        <input
                          type="checkbox"
                          checked={selectedPermissions.includes(permission.key)}
                          onChange={(event) => {
                            setSelectedPermissions((current) => event.target.checked
                              ? Array.from(new Set([...current, permission.key]))
                              : current.filter((item) => item !== permission.key));
                          }}
                          className="h-4 w-4 rounded border-white/20 bg-slate-950 text-brand-400 focus:ring-brand-400/30"
                        />
                        <span>{permission.label}</span>
                      </label>
                    ))}
                  </div>
                </div>
              </Field>
              <Button className="w-full" variant="secondary" onClick={() => void savePermission()} disabled={!canGrantSelectedPermissions || selectedPermissions.length === 0}>إضافة الصلاحيات المحددة</Button>

              <Field label="تعديل النقاط">
                <Input value={pointForm.amount} onChange={(event) => setPointForm((current) => ({ ...current, amount: event.target.value }))} type="number" />
              </Field>
              <Field label="سبب التعديل">
                <Input value={pointForm.reason} onChange={(event) => setPointForm((current) => ({ ...current, reason: event.target.value }))} placeholder="مكافأة / خصم / نشاط" />
              </Field>
              <Button className="w-full" variant="secondary" onClick={() => void savePoints()} disabled={!canManageUsers}>تعديل النقاط</Button>

              <Button className="w-full" variant="danger" onClick={() => void resetMemberPassword()} disabled={!canManageUsers}>إعادة تعيين كلمة المرور</Button>
              <Button className="w-full" variant="danger" onClick={() => void removeMember()} disabled={!canDeleteSelectedMember}>
                <span className="inline-flex items-center gap-2"><FiTrash2 /> حذف العضو</span>
              </Button>
            </div>
          ) : (
            <EmptyState title="اختر عضوًا" description="حدد عضوًا من الجدول لعرض أدوات الإدارة السريعة." />
          )}
        </Card>
      </div>

      <Modal
        open={createOpen}
        onClose={() => { setCreateOpen(false); setMemberFormError(''); }}
        title="إنشاء حساب داخلي"
        subtitle="Default password: Test123."
        footer={<><Button variant="ghost" onClick={() => { setCreateOpen(false); setMemberFormError(''); }}><span className="inline-flex items-center gap-2"><FiArrowLeft /> إلغاء</span></Button><Button type="submit" form="member-create-form"><span className="inline-flex items-center gap-2"><FiSave /> إنشاء</span></Button></>}
      >
        <form id="member-create-form" className="grid gap-4 md:grid-cols-2" onSubmit={(event) => void saveMember(event)}>
          {memberFormError && <div className="md:col-span-2 rounded-2xl border border-rose-400/20 bg-rose-400/10 px-4 py-3 text-sm text-rose-200">{memberFormError}</div>}
          <Field label="الاسم رباعي" hint="مثال: محمد علي محمود أحمد"><Input value={memberForm.fullName} onChange={(event) => setMemberForm((current) => ({ ...current, fullName: event.target.value }))} placeholder="الاسم الكامل" /></Field>
          <Field label="البريد الإلكتروني" hint="سيُستخدم لتسجيل الدخول"><Input value={memberForm.email} onChange={(event) => setMemberForm((current) => ({ ...current, email: event.target.value }))} type="email" placeholder="example@domain.com" /></Field>
          <Field label="الرقم القومي" hint="يُستخرج منه تاريخ الميلاد تلقائياً"><Input value={memberForm.nationalId} onChange={(event) => {
            const newId = event.target.value;
            setMemberForm((current) => {
              const updated = { ...current, nationalId: newId };
              const extractedDate = extractBirthDateFromNationalId(newId);
              if (extractedDate) {
                updated.birthDate = extractedDate;
              }
              return updated;
            });
          }} inputMode="numeric" maxLength={14} placeholder="14 رقمًا" /></Field>
          <Field label="تاريخ الميلاد" hint={memberForm.birthDate ? `تُم استخراجه من الرقم القومي: ${memberForm.birthDate.replace(/-/g, '/')}` : 'يملأ تلقائياً من الرقم القومي'}><Input value={memberForm.birthDate} onChange={(event) => setMemberForm((current) => ({ ...current, birthDate: event.target.value }))} type="date" disabled={memberForm.nationalId.length === 14} /></Field>
          <Field label="الدور" className="md:col-span-2">
            <Select value={memberForm.role} onChange={(event) => setMemberForm((current) => ({ ...current, role: event.target.value as Role }))}>
              {Object.keys(roleLabels).map((role) => <option key={role} value={role}>{roleLabel(role)}</option>)}
            </Select>
          </Field>
          {roleNeedsGovernorate(memberForm.role) && (
            <Field label="المحافظة">
              <Select
                value={memberForm.governorateId}
                onChange={(event) => setMemberForm((current) => ({ ...current, governorateId: event.target.value, committeeId: '' }))}
                  disabled={scopeLoading && visibleGovernorates.length === 0}
              >
                <option value="">اختر المحافظة</option>
                  {visibleGovernorates.map((governorate) => <option key={governorate.governorateId} value={governorate.governorateId}>{governorate.name}</option>)}
              </Select>
            </Field>
          )}
          {roleNeedsCommittee(memberForm.role) && (
            <Field label="اللجنة">
              <Select
                value={memberForm.committeeId}
                onChange={(event) => setMemberForm((current) => ({ ...current, committeeId: event.target.value }))}
                disabled={!memberForm.governorateId || scopeLoading}
              >
                <option value="">اختر اللجنة</option>
                {committees.map((committee) => <option key={committee.committeeId} value={committee.committeeId}>{committee.name}</option>)}
              </Select>
            </Field>
          )}
        </form>
      </Modal>
    </div>
  );
}

function TasksPage() {
  const { tasks, members, search, createTaskItem, updateTaskItem, deleteTaskItem, completeTaskItem, user } = useApp();
  const [page, setPage] = useState(1);
  const [taskOpen, setTaskOpen] = useState(false);
  const [editing, setEditing] = useState<TaskItem | null>(null);
  const [taskForm, setTaskForm] = useState<TaskFormState>(emptyTask);
  const roleOptions = useMemo(() => Object.keys(roleLabels) as Role[], []);

  const filtered = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    return tasks.filter((task) => [
      task.title,
      task.description ?? '',
      task.isCompleted ? 'مكتملة' : 'قيد التنفيذ',
      taskAudienceLabel(task.audienceType),
      task.targetRoles.map((role) => roleLabels[role] ?? role).join(' '),
      task.targetMemberIds.join(' ')
    ].join(' ').toLowerCase().includes(normalized));
  }, [search, tasks]);

  const selectedRoles = taskForm.targetRoles;
  const selectedMembers = taskForm.targetMemberIds;
  const isCommitteeCoordinator = user?.role === 'GovernorCommitteeCoordinator';
  const isGovernorCoordinator = user?.role === 'GovernorCoordinator';
  const hasRestrictedTaskAudience = isCommitteeCoordinator || isGovernorCoordinator;

  const visibleMemberOptions = useMemo(() => {
    if (isCommitteeCoordinator) {
      return members.filter((member) =>
        member.role === 'CommitteeMember'
        && member.governorName === user?.governorName
        && member.committeeName === user?.committeeName
      );
    }

    if (isGovernorCoordinator) {
      return members.filter((member) =>
        member.role === 'GovernorCommitteeCoordinator'
        && member.governorName === user?.governorName
      );
    }

    {
      return members;
    }
  }, [isCommitteeCoordinator, isGovernorCoordinator, members, user?.committeeName, user?.governorName]);

  const columns: TableColumn<TaskItem>[] = [
    { header: 'المهمة', render: (row) => <div><p className="font-bold text-white">{row.title}</p><p className="text-xs text-slate-400">{row.description ?? 'لا يوجد وصف'}</p></div> },
    { header: 'التصنيف', render: (row) => <div className="space-y-1"><Badge tone="brand">{taskAudienceLabel(row.audienceType)}</Badge><p className="text-xs text-slate-400">{row.audienceType === 'All' ? 'متاحة للجميع' : row.audienceType === 'Roles' ? `${row.targetRoles.length} منصب` : `${row.targetMemberIds.length} عضو`}</p></div> },
    { header: 'الحالة', render: (row) => <Badge tone={row.isCompleted ? 'success' : 'warning'}>{row.isCompleted ? 'مكتملة' : 'قيد التنفيذ'}</Badge> },
    { header: 'الاستحقاق', render: (row) => formatDate(row.dueDate) },
    {
      header: 'الإجراءات',
      render: (row) => (
        <div className="flex flex-wrap gap-2">
          {row.createdByMemberId === user?.id && (
            <Button variant="ghost" onClick={() => { setEditing(row); setTaskForm({ title: row.title, description: row.description ?? '', dueDate: row.dueDate?.slice(0, 10) ?? '', audienceType: (row.audienceType as TaskAudienceType) ?? 'All', targetRoles: row.targetRoles as Role[], targetMemberIds: row.targetMemberIds, isCompleted: row.isCompleted }); setTaskOpen(true); }}>
              <span className="inline-flex items-center gap-2"><FiEdit3 /> تعديل</span>
            </Button>
          )}
          {!row.isCompleted && (
            <Button variant="secondary" onClick={() => void completeTaskItem(row.id)}>
              <span className="inline-flex items-center gap-2"><FiSave /> إتمام المهمة</span>
            </Button>
          )}
          {row.createdByMemberId === user?.id && (
            <Button variant="danger" onClick={() => void deleteTaskItem(row.id)}>
              <span className="inline-flex items-center gap-2"><FiTrash2 /> حذف</span>
            </Button>
          )}
        </div>
      )
    }
  ];

  const syncAudience = (audienceType: TaskAudienceType) => {
    if (hasRestrictedTaskAudience) {
      audienceType = 'Members';
    }

    setTaskForm((current) => ({
      ...current,
      audienceType,
      targetRoles: audienceType === 'Roles' ? current.targetRoles : [],
      targetMemberIds: audienceType === 'Members' ? current.targetMemberIds : []
    }));
  };

  const saveTask = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const formToSave = hasRestrictedTaskAudience ? { ...taskForm, audienceType: 'Members' as TaskAudienceType, targetRoles: [] } : taskForm;
    if (editing) {
      await updateTaskItem(editing.id, formToSave);
    } else {
      await createTaskItem(formToSave);
    }
    setEditing(null);
    setTaskOpen(false);
    setTaskForm(emptyTask);
  };

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.tasks.eyebrow}
        title={pageTitles.tasks.title}
        description={pageTitles.tasks.description}
        actions={<Button onClick={() => { setEditing(null); setTaskForm(hasRestrictedTaskAudience ? { ...emptyTask, audienceType: 'Members' } : emptyTask); setTaskOpen(true); }}><span className="inline-flex items-center gap-2"><FiPlus /> مهمة جديدة</span></Button>}
      />

      <Card title="قائمة المهام" subtitle="Tasks">
        <PagedTable
          rows={filtered}
          columns={columns}
          rowKey={(row) => row.id}
          page={page}
          pageSize={6}
          onPageChange={setPage}
          search={search}
          emptyTitle="لا توجد مهام"
          emptyDescription="سيظهر جدول المهام هنا فور توفر البيانات أو بعد إنشاء أول مهمة."
        />
      </Card>

      <Modal
        open={taskOpen}
        onClose={() => { setTaskOpen(false); setEditing(null); setTaskForm(hasRestrictedTaskAudience ? { ...emptyTask, audienceType: 'Members' } : emptyTask); }}
        title={editing ? 'تعديل مهمة' : 'إضافة مهمة'}
        subtitle="Task form"
        footer={<><Button variant="ghost" onClick={() => { setTaskOpen(false); setEditing(null); setTaskForm(hasRestrictedTaskAudience ? { ...emptyTask, audienceType: 'Members' } : emptyTask); }}>إلغاء</Button><Button type="submit" form="task-form"><span className="inline-flex items-center gap-2"><FiSave /> حفظ</span></Button></>}
      >
        <form id="task-form" className="grid gap-4 md:grid-cols-2" onSubmit={(event) => void saveTask(event)}>
          <Field label="العنوان"><Input value={taskForm.title} onChange={(event) => setTaskForm((current) => ({ ...current, title: event.target.value }))} /></Field>
          <Field label="تاريخ الاستحقاق"><Input value={taskForm.dueDate} onChange={(event) => setTaskForm((current) => ({ ...current, dueDate: event.target.value }))} type="date" /></Field>
          <Field label="تصنيف المهمة" className="md:col-span-2">
            {hasRestrictedTaskAudience ? (
              <div className="rounded-2xl border border-white/10 bg-white/5 px-4 py-3 text-sm text-slate-200">لأعضاء معينين</div>
            ) : (
              <Select value={taskForm.audienceType} onChange={(event) => syncAudience(event.target.value as TaskAudienceType)}>
                <option value="All">للجميع</option>
                <option value="Members">لأعضاء معينين</option>
                <option value="Roles">لمناصب معينة</option>
              </Select>
            )}
          </Field>
          {taskForm.audienceType === 'Roles' && !hasRestrictedTaskAudience && (
            <Field label="المناصب المستهدفة" className="md:col-span-2">
              <div className="max-h-56 overflow-auto rounded-2xl border border-white/10 bg-white/5 p-3">
                <div className="grid gap-2">
                  {roleOptions.map((role) => (
                    <label key={role} className="flex cursor-pointer items-center gap-3 rounded-xl bg-slate-950/45 px-3 py-2 text-sm text-slate-200 transition hover:bg-slate-950/70">
                      <input
                        type="checkbox"
                        checked={selectedRoles.includes(role)}
                        onChange={(event) => setTaskForm((current) => ({
                          ...current,
                          targetRoles: event.target.checked
                            ? Array.from(new Set([...current.targetRoles, role]))
                            : current.targetRoles.filter((item) => item !== role)
                        }))}
                        className="h-4 w-4 rounded border-white/20 bg-slate-950 text-brand-400 focus:ring-brand-400/30"
                      />
                      <span>{roleLabels[role]}</span>
                    </label>
                  ))}
                </div>
              </div>
            </Field>
          )}
          {(taskForm.audienceType === 'Members' || hasRestrictedTaskAudience) && (
            <Field label="الأعضاء المستهدفين" className="md:col-span-2">
              <div className="max-h-56 overflow-auto rounded-2xl border border-white/10 bg-white/5 p-3">
                {visibleMemberOptions.length > 0 ? (
                  <div className="grid gap-2">
                    {visibleMemberOptions.map((member) => (
                      <label key={member.memberId} className="flex cursor-pointer items-center gap-3 rounded-xl bg-slate-950/45 px-3 py-2 text-sm text-slate-200 transition hover:bg-slate-950/70">
                        <input
                          type="checkbox"
                          checked={selectedMembers.includes(member.memberId)}
                          onChange={(event) => setTaskForm((current) => ({
                            ...current,
                            targetMemberIds: event.target.checked
                              ? Array.from(new Set([...current.targetMemberIds, member.memberId]))
                              : current.targetMemberIds.filter((item) => item !== member.memberId)
                          }))}
                          className="h-4 w-4 rounded border-white/20 bg-slate-950 text-brand-400 focus:ring-brand-400/30"
                        />
                        <span className="truncate">{member.fullName}</span>
                      </label>
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-slate-400">لا توجد قائمة أعضاء متاحة لديك لاختيارها.</p>
                )}
              </div>
            </Field>
          )}
          <Field label="الوصف" className="md:col-span-2"><Textarea value={taskForm.description} onChange={(event) => setTaskForm((current) => ({ ...current, description: event.target.value }))} rows={4} /></Field>
          <label className="flex items-center gap-3 md:col-span-2"><input checked={taskForm.isCompleted} onChange={(event) => setTaskForm((current) => ({ ...current, isCompleted: event.target.checked }))} type="checkbox" className="h-5 w-5 rounded border-white/20 bg-slate-900 text-brand-500" /><span className="text-sm font-semibold text-slate-200">مهمة مكتملة</span></label>
        </form>
      </Modal>
    </div>
  );
}

function ComplaintsPage() {
  const { complaints, myComplaints, search, canManageComplaints, createComplaintItem, reviewComplaintItem, refresh } = useApp();
  const [page, setPage] = useState(1);
  const [openForm, setOpenForm] = useState(false);
  const [form, setForm] = useState<ComplaintFormState>(emptyComplaint);
  const [reviews, setReviews] = useState<Record<string, ComplaintReviewState>>({});
  const [selectedComplaintId, setSelectedComplaintId] = useState('');
  const [selectedComplaint, setSelectedComplaint] = useState<ComplaintDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [commentForm, setCommentForm] = useState<ComplaintCommentState>({ notes: '' });
  const [escalateForm, setEscalateForm] = useState<ComplaintEscalateState>({ notes: '' });

  const filteredMine = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    return myComplaints.filter((item) => [item.subject, item.message, item.status, item.priority, item.escalationLevel].join(' ').toLowerCase().includes(normalized));
  }, [myComplaints, search]);

  const filteredAdmin = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    return complaints.filter((item) => [item.subject, item.message, item.memberName, item.status, item.priority].join(' ').toLowerCase().includes(normalized));
  }, [complaints, search]);

  useEffect(() => {
    if (!selectedComplaintId) {
      const firstItem = filteredAdmin[0] ?? filteredMine[0];
      if (firstItem) {
        setSelectedComplaintId(firstItem.id);
      }
    }
  }, [filteredAdmin, filteredMine, selectedComplaintId]);

  useEffect(() => {
    let cancelled = false;

    const loadDetail = async () => {
      if (!selectedComplaintId) {
        setSelectedComplaint(null);
        return;
      }

      setDetailLoading(true);
      try {
        const detail = await getComplaint(selectedComplaintId);
        if (!cancelled) {
          setSelectedComplaint(detail);
        }
      } catch {
        if (!cancelled) {
          setSelectedComplaint(null);
        }
      } finally {
        if (!cancelled) {
          setDetailLoading(false);
        }
      }
    };

    void loadDetail();

    return () => {
      cancelled = true;
    };
  }, [selectedComplaintId]);

  const saveComplaint = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await createComplaintItem(form);
    setForm(emptyComplaint);
    setOpenForm(false);
    await refresh();
  };

  const submitReview = async (item: ComplaintItem) => {
    const review = reviews[item.id] ?? { status: item.status, adminReply: item.adminReply ?? '' };
    await reviewComplaintItem(item.id, review);
    await refresh();
    if (selectedComplaintId === item.id) {
      setSelectedComplaint(await getComplaint(item.id));
    }
  };

  const submitComment = async () => {
    if (!selectedComplaint || !commentForm.notes.trim()) {
      return;
    }

    await commentComplaint(selectedComplaint.id, commentForm);
    setCommentForm({ notes: '' });
    await refresh();
    setSelectedComplaint(await getComplaint(selectedComplaint.id));
  };

  const submitEscalation = async () => {
    if (!selectedComplaint) {
      return;
    }

    await escalateComplaint(selectedComplaint.id, escalateForm);
    setEscalateForm({ notes: '' });
    await refresh();
    setSelectedComplaint(await getComplaint(selectedComplaint.id));
  };

  const currentDetail = selectedComplaint;

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.complaints.eyebrow}
        title={pageTitles.complaints.title}
        description={pageTitles.complaints.description}
        actions={<Button onClick={() => setOpenForm(true)}><span className="inline-flex items-center gap-2"><FiSend /> شكوى جديدة</span></Button>}
      />

      <div className="grid gap-6 xl:grid-cols-2">
        <Card title="شكاواي" subtitle="My complaints">
          <PagedTable
            rows={filteredMine}
            columns={[
              { header: 'الموضوع', render: (row: ComplaintItem) => <div><p className="font-bold text-white">{row.subject}</p><p className="text-xs text-slate-400">{row.message}</p></div> },
              { header: 'الأولوية', render: (row: ComplaintItem) => <Badge tone={row.priority === 'High' ? 'danger' : row.priority === 'Medium' ? 'warning' : 'neutral'}>{priorityLabel(row.priority)}</Badge> },
              { header: 'المستوى', render: (row: ComplaintItem) => <Badge tone="brand">{row.escalationLevel}</Badge> },
              { header: 'رد الإدارة', render: (row: ComplaintItem) => row.adminReply ? <Badge tone="success">تم الرد</Badge> : <Badge tone="neutral">لا يوجد رد</Badge> },
              { header: 'الحالة', render: (row: ComplaintItem) => <Badge tone={row.status === 'Resolved' ? 'success' : row.status === 'Rejected' ? 'danger' : 'warning'}>{statusLabel(row.status)}</Badge> },
              { header: 'آخر تحديث', render: (row: ComplaintItem) => formatDate(row.updatedAtUtc) },
              { header: 'تفاصيل', render: (row: ComplaintItem) => <Button variant="ghost" onClick={() => setSelectedComplaintId(row.id)}>عرض</Button> }
            ]}
            rowKey={(row) => row.id}
            page={page}
            pageSize={5}
            onPageChange={setPage}
            search={search}
            emptyTitle="لا توجد شكاوى"
            emptyDescription="سجل الشكاوى الشخصية سيظهر هنا بعد أول عملية إرسال."
          />
        </Card>

        {canManageComplaints ? (
          <Card title="الشكاوى الواردة" subtitle="Admin review">
            <div className="space-y-4">
              {filteredAdmin.length === 0 ? (
                <EmptyState title="لا توجد شكاوى واردة" description="لن ترى هذا القسم إلا عند وجود شكاوى تتطلب المراجعة." />
              ) : filteredAdmin.map((item) => (
                <div key={item.id} className="rounded-3xl border border-white/10 bg-white/5 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="font-bold text-white">{item.subject}</p>
                      <p className="text-sm text-slate-400">{item.memberName}</p>
                    </div>
                    <Badge tone={item.status === 'Resolved' ? 'success' : item.status === 'Rejected' ? 'danger' : 'warning'}>{statusLabel(item.status)}</Badge>
                  </div>
                  <div className="mt-2 flex flex-wrap gap-2 text-xs text-slate-400">
                    <span>الأولوية: {priorityLabel(item.priority)}</span>
                    <span>المستوى: {item.escalationLevel}</span>
                    <span>آخر إجراء: {formatDate(item.lastActionDateUtc)}</span>
                  </div>
                  <p className="mt-3 text-sm leading-7 text-slate-300">{item.message}</p>
                  <div className="mt-4 grid gap-3 md:grid-cols-2">
                    <Select value={reviews[item.id]?.status ?? item.status} onChange={(event) => setReviews((current) => ({ ...current, [item.id]: { status: event.target.value, adminReply: current[item.id]?.adminReply ?? item.adminReply ?? '' } }))}>
                      {['Open', 'InReview', 'Resolved', 'Rejected'].map((status) => <option key={status} value={status}>{statusLabel(status)}</option>)}
                    </Select>
                    <Input value={reviews[item.id]?.adminReply ?? item.adminReply ?? ''} onChange={(event) => setReviews((current) => ({ ...current, [item.id]: { status: current[item.id]?.status ?? item.status, adminReply: event.target.value } }))} placeholder="رد إداري" />
                  </div>
                  <div className="mt-4 flex flex-wrap justify-end gap-2">
                    <Button variant="ghost" onClick={() => setSelectedComplaintId(item.id)}>عرض التفاصيل</Button>
                    <Button onClick={() => void submitReview(item)}><span className="inline-flex items-center gap-2"><FiSave /> حفظ</span></Button>
                  </div>
                </div>
              ))}
            </div>
          </Card>
        ) : (
          <Card title="مراجعة الشكاوى" subtitle="Admin review">
            <EmptyState title="لا صلاحية للمراجعة" description="هذا القسم يظهر لمن لديهم صلاحية إدارة الشكاوى فقط." />
          </Card>
        )}
      </div>

      <Card title="تفاصيل الشكوى" subtitle="Complaint timeline">
        {detailLoading ? (
          <EmptyState title="جاري التحميل" description="يتم الآن جلب تفاصيل الشكوى والتاريخ الخاص بها." />
        ) : currentDetail ? (
          <div className="grid gap-6 xl:grid-cols-[1fr_0.9fr]">
            <div className="space-y-4">
              <div className="rounded-3xl border border-white/10 bg-white/5 p-4">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge tone="brand">{statusLabel(currentDetail.status)}</Badge>
                  <Badge tone={currentDetail.priority === 'High' ? 'danger' : currentDetail.priority === 'Medium' ? 'warning' : 'neutral'}>{priorityLabel(currentDetail.priority)}</Badge>
                  <Badge tone="success">المستوى {currentDetail.escalationLevel}</Badge>
                </div>
                <p className="mt-3 text-lg font-bold text-white">{currentDetail.subject}</p>
                <p className="mt-2 text-sm leading-7 text-slate-300">{currentDetail.message}</p>
                <div className="mt-4 grid gap-2 text-sm text-slate-400 sm:grid-cols-2">
                  <p>مقدم الشكوى: {currentDetail.memberName}</p>
                  <p>المعيّن الحالي: {currentDetail.assignedToMemberName ?? 'غير معين'}</p>
                  <p>آخر إجراء: {formatDate(currentDetail.lastActionDateUtc)}</p>
                  <p>الرد الإداري: {currentDetail.adminReply ?? '—'}</p>
                </div>
                {currentDetail.adminReply && (
                  <div className="mt-4 rounded-2xl border border-emerald-400/20 bg-emerald-400/10 p-4">
                    <p className="text-sm font-bold text-emerald-200">وصل رد للشكوى</p>
                    <p className="mt-2 text-sm leading-7 text-slate-100">{currentDetail.adminReply}</p>
                  </div>
                )}
              </div>

              <div className="space-y-3">
                {currentDetail.history.length === 0 ? (
                  <EmptyState title="لا يوجد تاريخ بعد" description="سيظهر خط الزمن هنا بعد أول إجراء على الشكوى." />
                ) : currentDetail.history.map((entry) => (
                  <div key={entry.id} className="rounded-3xl border border-white/10 bg-white/5 p-4">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex items-center gap-2">
                        <Badge tone="brand">{entry.action}</Badge>
                        <p className="font-bold text-white">{entry.performedByUserName ?? 'النظام'}</p>
                      </div>
                      <span className="text-xs text-slate-400">{formatDate(entry.timestampUtc)}</span>
                    </div>
                    <p className="mt-2 text-sm leading-7 text-slate-300">{entry.notes ?? '—'}</p>
                  </div>
                ))}
              </div>
            </div>

            <div className="space-y-4">
              <Card title="تعليق" subtitle="Add comment" className="bg-white/5">
                <div className="space-y-4">
                  <Textarea value={commentForm.notes} onChange={(event) => setCommentForm((current) => ({ ...current, notes: event.target.value }))} rows={4} placeholder="اكتب تعليقًا على الشكوى" />
                  <Button className="w-full" variant="secondary" onClick={() => void submitComment()}>حفظ التعليق</Button>
                </div>
              </Card>

              <Card title="تصعيد يدوي" subtitle="Manual escalation" className="bg-white/5">
                <div className="space-y-4">
                  <Textarea value={escalateForm.notes} onChange={(event) => setEscalateForm((current) => ({ ...current, notes: event.target.value }))} rows={4} placeholder="سبب التصعيد" />
                  <Button className="w-full" onClick={() => void submitEscalation()} disabled={!canManageComplaints || currentDetail.escalationLevel >= 3 || currentDetail.status === 'Resolved'}>
                    <span className="inline-flex items-center gap-2"><FiArrowLeft /> تصعيد الشكوى</span>
                  </Button>
                </div>
              </Card>

              <Card title="معلومات إضافية" subtitle="Metadata" className="bg-white/5">
                <div className="space-y-2 text-sm text-slate-300">
                  <p>تم الإنشاء: {formatDate(currentDetail.createdAtUtc)}</p>
                  <p>آخر تحديث: {formatDate(currentDetail.updatedAtUtc)}</p>
                  <p>المستوى الحالي: {currentDetail.escalationLevel}</p>
                  <p>الأولوية: {priorityLabel(currentDetail.priority)}</p>
                </div>
              </Card>
            </div>
          </div>
        ) : (
          <EmptyState title="اختر شكوى" description="حدد شكوى من القائمة أو من الشكاوى الواردة لعرض التاريخ والتصعيد والتعليقات." />
        )}
      </Card>

      <Modal
        open={openForm}
        onClose={() => setOpenForm(false)}
        title="إرسال شكوى"
        subtitle="Complaint form"
        footer={<><Button variant="ghost" onClick={() => setOpenForm(false)}>إلغاء</Button><Button type="submit" form="complaint-form"><span className="inline-flex items-center gap-2"><FiSend /> إرسال</span></Button></>}
      >
        <form id="complaint-form" className="space-y-4" onSubmit={(event) => void saveComplaint(event)}>
          <Field label="الموضوع"><Input value={form.subject} onChange={(event) => setForm((current) => ({ ...current, subject: event.target.value }))} /></Field>
          <Field label="الأولوية">
            <Select value={form.priority} onChange={(event) => setForm((current) => ({ ...current, priority: event.target.value as ComplaintFormState['priority'] }))}>
              <option value="Low">{priorityLabel('Low')}</option>
              <option value="Medium">{priorityLabel('Medium')}</option>
              <option value="High">{priorityLabel('High')}</option>
            </Select>
          </Field>
          <Field label="نص الشكوى"><Textarea value={form.message} onChange={(event) => setForm((current) => ({ ...current, message: event.target.value }))} rows={6} /></Field>
        </form>
      </Modal>
    </div>
  );
}

function CommitteesPage() {
  const { user, addActivity } = useApp();
  const [governorates, setGovernorates] = useState<GovernorateOption[]>([]);
  const [selectedGovernorateId, setSelectedGovernorateId] = useState('');
  const [committees, setCommittees] = useState<CommitteeOption[]>([]);
  const [form, setForm] = useState<CommitteeCreateFormState>(emptyCommittee);
  const [updatingGovernorateVisibility, setUpdatingGovernorateVisibility] = useState(false);
  const [updatingCommitteeVisibilityId, setUpdatingCommitteeVisibilityId] = useState<string | null>(null);

  const canManageCommitteeCatalog = user?.role === 'President' || user?.role === 'VicePresident' || user?.role === 'GovernorCoordinator';
  const canManageJoinVisibility = Boolean(user?.permissions.some((permission) => permission.toLowerCase() === 'joinrequests.visibility.manage'));
  const isCommitteeCoordinator = user?.role === 'GovernorCommitteeCoordinator';
  const canManageCommitteeJoinVisibility = canManageJoinVisibility || isCommitteeCoordinator;
  const canManageGovernorateJoinVisibility = canManageJoinVisibility && !isCommitteeCoordinator;
  const canAccessGovernorateControls = canManageCommitteeCatalog || canManageCommitteeJoinVisibility;
  const canManageAnyGovernorate = user?.role === 'President' || user?.role === 'VicePresident';
  const visibleGovernorates = useMemo(() => {
    if (canManageAnyGovernorate) {
      return governorates;
    }

    if (user?.governorName) {
      return governorates.filter((governorate) => governorate.name === user.governorName);
    }

    return [];
  }, [canManageAnyGovernorate, governorates, user?.governorName]);
  const selectedGovernorate = useMemo(
    () => governorates.find((governorate) => governorate.governorateId === selectedGovernorateId) ?? null,
    [governorates, selectedGovernorateId]
  );

  useEffect(() => {
    let cancelled = false;

    const loadGovernorates = async () => {
      try {
        const result = await getGovernorates();
        if (!cancelled) {
          setGovernorates(result);
          const filteredGovernorates = canManageAnyGovernorate
            ? result
            : user?.governorName
              ? result.filter((governorate) => governorate.name === user.governorName)
              : [];
          setSelectedGovernorateId((current) => current || filteredGovernorates[0]?.governorateId || '');
        }
      } catch {
        if (!cancelled) {
          setGovernorates([]);
          setSelectedGovernorateId('');
        }
      }
    };

    void loadGovernorates();

    return () => {
      cancelled = true;
    };
  }, [canManageAnyGovernorate, user?.governorName]);

  useEffect(() => {
    if (!selectedGovernorateId) {
      setCommittees([]);
      return;
    }

    let cancelled = false;

    const loadCommittees = async () => {
      try {
        const result = await getGovernorateCommittees(selectedGovernorateId);
        if (!cancelled) {
          const scopedCommittees = isCommitteeCoordinator && user?.committeeName
            ? result.filter((committee) => isSameScopeName(committee.name, user.committeeName))
            : result;

          setCommittees(scopedCommittees);
        }
      } catch {
        if (!cancelled) {
          setCommittees([]);
        }
      }
    };

    void loadCommittees();

    return () => {
      cancelled = true;
    };
  }, [isCommitteeCoordinator, selectedGovernorateId, user?.committeeName]);

  const addCommittee = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selectedGovernorateId || !form.name.trim()) {
      return;
    }

    try {
      await createCommittee(selectedGovernorateId, form);
      setForm(emptyCommittee);
      setCommittees(await getGovernorateCommittees(selectedGovernorateId));
      if (addActivity) {
        addActivity('إضافة لجنة', `تمت إضافة لجنة ${form.name}`, 'success');
      }
    } catch {
      return;
    }
  };

  const removeCommittee = async (committee: CommitteeOption) => {
    if (!canManageCommitteeCatalog) {
      return;
    }

    if (!window.confirm(`هل تريد حذف لجنة "${committee.name}"؟`)) {
      return;
    }

    try {
      await deleteCommittee(committee.governorateId, committee.committeeId);
      setCommittees((current) => current.filter((item) => item.committeeId !== committee.committeeId));
      if (addActivity) {
        addActivity('حذف لجنة', `تم حذف لجنة ${committee.name}`, 'warning');
      }
    } catch (error) {
      window.alert(error instanceof Error ? error.message : 'تعذر حذف اللجنة حاليًا.');
    }
  };

  const toggleJoinFormVisibility = async () => {
    if (!canManageGovernorateJoinVisibility) {
      return;
    }

    if (!selectedGovernorate) {
      return;
    }

    setUpdatingGovernorateVisibility(true);
    try {
      const updatedGovernorate = await updateGovernorateJoinVisibility(
        selectedGovernorate.governorateId,
        !selectedGovernorate.isVisibleInJoinForm
      );

      setGovernorates((current) =>
        current.map((governorate) =>
          governorate.governorateId === updatedGovernorate.governorateId ? updatedGovernorate : governorate
        )
      );

      if (addActivity) {
        addActivity(
          'تحديث إتاحة التقديم',
          `تم ${updatedGovernorate.isVisibleInJoinForm ? 'إظهار' : 'إخفاء'} محافظة ${updatedGovernorate.name} في فورم التقديم.`,
          updatedGovernorate.isVisibleInJoinForm ? 'success' : 'warning'
        );
      }
    } catch (error) {
      window.alert(error instanceof Error ? error.message : 'تعذر تحديث حالة الظهور حاليًا.');
    } finally {
      setUpdatingGovernorateVisibility(false);
    }
  };

  const toggleCommitteeFormVisibility = async (committee: CommitteeOption) => {
    if (!canManageCommitteeJoinVisibility) {
      return;
    }

    setUpdatingCommitteeVisibilityId(committee.committeeId);
    try {
      const updatedCommittee = await updateCommitteeJoinVisibility(
        committee.governorateId,
        committee.committeeId,
        !committee.isVisibleInJoinForm
      );

      setCommittees((current) =>
        current.map((item) =>
          item.committeeId === updatedCommittee.committeeId ? updatedCommittee : item
        )
      );

      if (addActivity) {
        addActivity(
          'تحديث إتاحة التقديم على لجنة',
          `تم ${updatedCommittee.isVisibleInJoinForm ? 'فتح' : 'إغلاق'} التقديم على لجنة ${updatedCommittee.name}.`,
          updatedCommittee.isVisibleInJoinForm ? 'success' : 'warning'
        );
      }
    } catch (error) {
      window.alert(error instanceof Error ? error.message : 'تعذر تحديث حالة التقديم على اللجنة حاليًا.');
    } finally {
      setUpdatingCommitteeVisibilityId(null);
    }
  };

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.committees.eyebrow}
        title={pageTitles.committees.title}
        description={pageTitles.committees.description}
      />

      <div className="grid gap-6 xl:grid-cols-[0.9fr_1.1fr]">
        <Card title="إنشاء لجنة" subtitle="Local workspace">
          {canAccessGovernorateControls ? (
            <form className="space-y-4" onSubmit={(event) => void addCommittee(event)}>
              <Field label="المحافظة">
                <Select value={selectedGovernorateId} onChange={(event) => setSelectedGovernorateId(event.target.value)}>
                  <option value="">اختر المحافظة</option>
                  {visibleGovernorates.map((governorate) => <option key={governorate.governorateId} value={governorate.governorateId}>{governorate.name}</option>)}
                </Select>
              </Field>

              {canManageGovernorateJoinVisibility ? (
                <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-sm text-slate-300">
                  <p className="text-xs text-slate-400">ظهور المحافظة في فورم التقديم</p>
                  <p className="mt-1 font-semibold text-white">
                    {!selectedGovernorate
                      ? 'اختر محافظة أولًا'
                      : selectedGovernorate.isVisibleInJoinForm
                        ? 'المحافظة ظاهرة ويمكن التقديم عليها'
                        : 'المحافظة مخفية والتقديم عليها متوقف'}
                  </p>
                  <p className="mt-2 text-xs text-slate-400">عند اكتمال العدد المطلوب يمكنك إخفاء المحافظة من فورم التقديم العام ثم إعادة تفعيلها لاحقًا.</p>
                  <Button
                    type="button"
                    variant={selectedGovernorate?.isVisibleInJoinForm ? 'ghost' : 'secondary'}
                    className="mt-3 w-full"
                    onClick={() => void toggleJoinFormVisibility()}
                    disabled={!selectedGovernorate || updatingGovernorateVisibility}
                  >
                    {updatingGovernorateVisibility
                      ? 'جارٍ تحديث الحالة...'
                      : selectedGovernorate?.isVisibleInJoinForm
                        ? 'إغلاق التقديم على المحافظة'
                        : 'فتح التقديم على المحافظة'}
                  </Button>
                </div>
              ) : canManageJoinVisibility ? (
                <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-xs leading-6 text-slate-400">
                  لا تملك صلاحية فتح/إغلاق التقديم على المحافظة.
                </div>
              ) : null}

              {!canManageCommitteeCatalog && canManageCommitteeJoinVisibility && (
                <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-xs leading-6 text-slate-400">
                  لديك صلاحية التحكم في فتح/إغلاق التقديم على اللجنة داخل نطاقك فقط.
                </div>
              )}

              {canManageCommitteeCatalog && (
                <>
                  <Field label="اسم اللجنة">
                    <Input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} placeholder="مثال: لجنة التنظيم" />
                  </Field>
                  <Button type="submit" className="w-full" disabled={!selectedGovernorateId}><span className="inline-flex items-center gap-2"><FiPlus /> إضافة لجنة</span></Button>
                </>
              )}
            </form>
          ) : (
            <EmptyState title="لا توجد صلاحية" description="لا توجد صلاحية لإدارة اللجان أو فتح/إغلاق التقديم على المحافظة." />
          )}
        </Card>

        <Card title="قائمة اللجان" subtitle="Committee roster">
          {committees.length === 0 ? (
            <EmptyState title="لا توجد لجان" description="اختر محافظة لعرض اللجان المسجلة داخلها." />
          ) : (
            <div className="space-y-3">
              {committees.map((committee) => (
                <div key={committee.committeeId} className="rounded-3xl border border-white/10 bg-white/5 p-4">
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div>
                      <p className="font-bold text-white">{committee.name}</p>
                      <p className="text-sm text-slate-400">{committee.governorateName}</p>
                      <p className="mt-1 text-xs text-slate-300">{committee.isVisibleInJoinForm ? 'التقديم مفتوح على هذه اللجنة' : 'التقديم مغلق على هذه اللجنة'}</p>
                      <p className="mt-2 text-xs text-slate-400">{formatDate(committee.createdAtUtc)}</p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {canManageCommitteeJoinVisibility && (
                        <Button
                          variant={committee.isVisibleInJoinForm ? 'ghost' : 'secondary'}
                          className="px-3 py-2"
                          onClick={() => void toggleCommitteeFormVisibility(committee)}
                          disabled={updatingCommitteeVisibilityId === committee.committeeId}
                        >
                          <span className="inline-flex items-center gap-2">
                            {updatingCommitteeVisibilityId === committee.committeeId
                              ? 'جارٍ التحديث...'
                              : committee.isVisibleInJoinForm
                                ? 'إغلاق التقديم'
                                : 'فتح التقديم'}
                          </span>
                        </Button>
                      )}
                      {canManageCommitteeCatalog && (
                        <Button variant="ghost" className="px-3 py-2" onClick={() => void removeCommittee(committee)}>
                          <span className="inline-flex items-center gap-2"><FiTrash2 /> حذف</span>
                        </Button>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}

function StudentClubsPage() {
  const { members, search, user, createMember, canCreateMembers, addActivity } = useApp();
  const [createClubOpen, setCreateClubOpen] = useState(false);
  const [createClubMemberOpen, setCreateClubMemberOpen] = useState(false);
  const [clubForm, setClubForm] = useState<CommitteeCreateFormState>(emptyCommittee);
  const [clubFormError, setClubFormError] = useState('');
  const [clubMemberForm, setClubMemberForm] = useState<MemberCreateFormState>({ ...emptyMember, role: 'CommitteeMember' });
  const [clubMemberFormError, setClubMemberFormError] = useState('');
  const [clubGovernorateId, setClubGovernorateId] = useState('');
  const [selectedClubGovernorateId, setSelectedClubGovernorateId] = useState('');
  const [governorates, setGovernorates] = useState<GovernorateOption[]>([]);
  const [committees, setCommittees] = useState<CommitteeOption[]>([]);
  const [clubCommittees, setClubCommittees] = useState<CommitteeOption[]>([]);
  const [updatingClubCommitteeVisibilityId, setUpdatingClubCommitteeVisibilityId] = useState<string | null>(null);
  const [scopeLoading, setScopeLoading] = useState(false);

  const clubRoles: Role[] = ['GovernorCoordinator', 'GovernorCommitteeCoordinator', 'CommitteeMember'];
  const canCreateClub = user?.role === 'President' || user?.role === 'VicePresident' || user?.role === 'GovernorCoordinator';
  const canManageClubJoinVisibility = Boolean(user?.permissions.some((permission) => permission.toLowerCase() === 'joinrequests.visibility.manage'))
    || user?.role === 'GovernorCommitteeCoordinator';
  const allowedClubCreateRoles = useMemo<Role[]>(() => {
    if (!canCreateMembers || !user) {
      return [];
    }

    if (user.role === 'GovernorCommitteeCoordinator') {
      return ['CommitteeMember'];
    }

    if (user.role === 'GovernorCoordinator') {
      return ['GovernorCommitteeCoordinator', 'CommitteeMember'];
    }

    return ['GovernorCoordinator', 'GovernorCommitteeCoordinator', 'CommitteeMember'];
  }, [canCreateMembers, user]);

  const canCreateClubMember = allowedClubCreateRoles.length > 0;
  const clubGovernoratesWithCommittees = useMemo(() => {
    return new Set(
      members
        .filter((member) => isClubCommitteeName(member.committeeName))
        .map((member) => normalizeScopeName(member.governorName))
        .filter((name) => name.length > 0)
    );
  }, [members]);

  const visibleGovernorates = useMemo(() => {
    if ((user?.role === 'GovernorCoordinator' || user?.role === 'GovernorCommitteeCoordinator') && user.governorName) {
      return governorates.filter((governorate) => isSameScopeName(governorate.name, user.governorName));
    }

    return governorates;
  }, [governorates, user?.governorName, user?.role]);

  const clubMembers = useMemo(() => {
    const normalized = search.trim().toLowerCase();

    let scopedMembers = members.filter((member) => {
      if (!clubRoles.includes(member.role)) {
        return false;
      }

      if (member.role === 'GovernorCoordinator') {
        return clubGovernoratesWithCommittees.has(normalizeScopeName(member.governorName));
      }

      return isClubCommitteeName(member.committeeName);
    });

    if (user?.role === 'GovernorCoordinator' && user.governorName) {
      scopedMembers = scopedMembers.filter((member) => isSameScopeName(member.governorName, user.governorName));
    }

    if (user?.role === 'GovernorCommitteeCoordinator' && user.governorName && user.committeeName) {
      scopedMembers = scopedMembers.filter((member) =>
        isSameScopeName(member.governorName, user.governorName)
        && isSameScopeName(member.committeeName, user.committeeName));
    }

    if (!normalized) {
      return scopedMembers;
    }

    return scopedMembers.filter((member) => [
      member.fullName,
      member.email,
      member.governorName ?? '',
      member.committeeName ?? '',
      clubRoleLabel(member.role)
    ].join(' ').toLowerCase().includes(normalized));
  }, [clubGovernoratesWithCommittees, members, search, user?.committeeName, user?.governorName, user?.role]);

  const roleStats = useMemo(() => {
    return {
      governorCoordinators: clubMembers.filter((member) => member.role === 'GovernorCoordinator').length,
      committeeCoordinators: clubMembers.filter((member) => member.role === 'GovernorCommitteeCoordinator').length,
      committeeMembers: clubMembers.filter((member) => member.role === 'CommitteeMember').length
    };
  }, [clubMembers]);

  useEffect(() => {
    if (!createClubOpen && !createClubMemberOpen && !canManageClubJoinVisibility) {
      return;
    }

    let cancelled = false;
    const loadGovernorates = async () => {
      setScopeLoading(true);
      try {
        const result = await getGovernorates();
        if (cancelled) {
          return;
        }

        setGovernorates(result);

        const scopedGovernorates = (user?.role === 'GovernorCoordinator' || user?.role === 'GovernorCommitteeCoordinator') && user.governorName
          ? result.filter((governorate) => isSameScopeName(governorate.name, user.governorName))
          : result;

        if (createClubOpen) {
          setClubGovernorateId((current) => current || scopedGovernorates[0]?.governorateId || '');
        }

        if (createClubMemberOpen) {
          setClubMemberForm((current) => ({
            ...current,
            role: allowedClubCreateRoles.includes(current.role) ? current.role : (allowedClubCreateRoles[0] ?? 'CommitteeMember'),
            governorateId: current.governorateId || scopedGovernorates[0]?.governorateId || ''
          }));
        }

        if (canManageClubJoinVisibility) {
          setSelectedClubGovernorateId((current) => current || scopedGovernorates[0]?.governorateId || '');
        }
      } catch {
        if (!cancelled) {
          setGovernorates([]);
        }
      } finally {
        if (!cancelled) {
          setScopeLoading(false);
        }
      }
    };

    void loadGovernorates();

    return () => {
      cancelled = true;
    };
  }, [allowedClubCreateRoles, canManageClubJoinVisibility, createClubMemberOpen, createClubOpen, user?.governorName, user?.role]);

  useEffect(() => {
    if (!createClubMemberOpen || !clubMemberForm.governorateId || !roleNeedsCommittee(clubMemberForm.role)) {
      setCommittees([]);
      return;
    }

    let cancelled = false;
    const loadCommittees = async () => {
      setScopeLoading(true);
      try {
        const result = await getGovernorateCommittees(clubMemberForm.governorateId, 'club');
        if (cancelled) {
          return;
        }

        const scopedCommittees = user?.role === 'GovernorCommitteeCoordinator' && user.committeeName
          ? result.filter((committee) => isSameScopeName(committee.name, user.committeeName))
          : result;

        setCommittees(scopedCommittees);
        if (user?.role === 'GovernorCommitteeCoordinator' && user.committeeName) {
          const scopedCommittee = scopedCommittees.find((committee) => isSameScopeName(committee.name, user.committeeName));
          setClubMemberForm((current) => ({
            ...current,
            committeeId: current.committeeId || scopedCommittee?.committeeId || ''
          }));
        }
      } catch {
        if (!cancelled) {
          setCommittees([]);
        }
      } finally {
        if (!cancelled) {
          setScopeLoading(false);
        }
      }
    };

    void loadCommittees();

    return () => {
      cancelled = true;
    };
  }, [clubMemberForm.governorateId, clubMemberForm.role, createClubMemberOpen, user?.committeeName, user?.role]);

  useEffect(() => {
    if (!createClubMemberOpen) {
      return;
    }

    if (!roleNeedsCommittee(clubMemberForm.role) && clubMemberForm.committeeId) {
      setClubMemberForm((current) => ({ ...current, committeeId: '' }));
    }
  }, [clubMemberForm.committeeId, clubMemberForm.role, createClubMemberOpen]);

  useEffect(() => {
    if (!canManageClubJoinVisibility || !selectedClubGovernorateId) {
      setClubCommittees([]);
      return;
    }

    let cancelled = false;
    const loadClubCommittees = async () => {
      try {
        const result = await getGovernorateCommittees(selectedClubGovernorateId, 'club');
        if (cancelled) {
          return;
        }

        const scopedCommittees = user?.role === 'GovernorCommitteeCoordinator' && user.committeeName
          ? result.filter((committee) => isSameScopeName(committee.name, user.committeeName))
          : result;

        setClubCommittees(scopedCommittees);
      } catch {
        if (!cancelled) {
          setClubCommittees([]);
        }
      }
    };

    void loadClubCommittees();

    return () => {
      cancelled = true;
    };
  }, [canManageClubJoinVisibility, selectedClubGovernorateId, user?.committeeName, user?.role]);

  const openCreateClubModal = () => {
    setClubForm(emptyCommittee);
    setClubFormError('');
    setClubGovernorateId(visibleGovernorates[0]?.governorateId ?? '');
    setCreateClubOpen(true);
  };

  const openCreateClubMemberModal = () => {
    const defaultRole = allowedClubCreateRoles[0] ?? 'CommitteeMember';
    const defaultGovernorate = visibleGovernorates[0]?.governorateId ?? '';

    setClubMemberForm({
      ...emptyMember,
      role: defaultRole,
      governorateId: defaultGovernorate,
      committeeId: ''
    });
    setClubMemberFormError('');
    setCreateClubMemberOpen(true);
  };

  const submitCreateClub = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setClubFormError('');

    if (!clubGovernorateId) {
      setClubFormError('يرجى اختيار المحافظة أولاً.');
      return;
    }

    if (!clubForm.name.trim()) {
      setClubFormError('اسم النادي الطلابي مطلوب.');
      return;
    }

    try {
      const trimmedName = clubForm.name.trim();
      const normalizedClubName = isClubCommitteeName(trimmedName) ? trimmedName : `نادي ${trimmedName}`;
      await createCommittee(clubGovernorateId, { name: normalizedClubName }, true);
      addActivity('إنشاء نادي طلابي', `تم إنشاء نادي ${normalizedClubName}.`, 'success');

      if (canManageClubJoinVisibility && selectedClubGovernorateId && selectedClubGovernorateId === clubGovernorateId) {
        const refreshedCommittees = await getGovernorateCommittees(selectedClubGovernorateId, 'club');
        setClubCommittees(refreshedCommittees);
      }

      setCreateClubOpen(false);
      setClubForm(emptyCommittee);
      setClubFormError('');
    } catch (error) {
      setClubFormError(error instanceof Error ? error.message : 'تعذر إنشاء النادي الطلابي.');
    }
  };

  const toggleClubFormVisibility = async (committee: CommitteeOption) => {
    if (!canManageClubJoinVisibility) {
      return;
    }

    setUpdatingClubCommitteeVisibilityId(committee.committeeId);
    try {
      const updatedCommittee = await updateCommitteeJoinVisibility(
        committee.governorateId,
        committee.committeeId,
        !committee.isVisibleInJoinForm
      );

      setClubCommittees((current) =>
        current.map((item) => item.committeeId === updatedCommittee.committeeId ? updatedCommittee : item)
      );

      setCommittees((current) =>
        current.map((item) => item.committeeId === updatedCommittee.committeeId ? updatedCommittee : item)
      );

      if (addActivity) {
        addActivity(
          'فورم نادي طلابي',
          `تم ${updatedCommittee.isVisibleInJoinForm ? 'فتح' : 'إغلاق'} فورم التقديم على ${updatedCommittee.name}.`,
          updatedCommittee.isVisibleInJoinForm ? 'success' : 'warning'
        );
      }
    } catch (error) {
      window.alert(error instanceof Error ? error.message : 'تعذر تحديث حالة الفورم حاليًا.');
    } finally {
      setUpdatingClubCommitteeVisibilityId(null);
    }
  };

  const submitCreateClubMember = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setClubMemberFormError('');

    if (!allowedClubCreateRoles.includes(clubMemberForm.role)) {
      setClubMemberFormError('لا تملك صلاحية إنشاء هذا الدور في النوادي الطلابية.');
      return;
    }

    const nameParts = clubMemberForm.fullName.trim().split(/\s+/).filter(Boolean);
    if (!clubMemberForm.fullName.trim() || nameParts.length < 4) {
      setClubMemberFormError('الاسم الرباعي مطلوب لإنشاء عضو النادي الطلابي.');
      return;
    }

    if (!clubMemberForm.email.trim()) {
      setClubMemberFormError('البريد الإلكتروني مطلوب.');
      return;
    }

    const normalizedNationalId = clubMemberForm.nationalId.replace(/\s+/g, '');
    if (normalizedNationalId.length !== 14 || /[^0-9]/.test(normalizedNationalId)) {
      setClubMemberFormError('الرقم القومي يجب أن يكون 14 رقمًا.');
      return;
    }

    let resolvedBirthDate = clubMemberForm.birthDate;
    if (!resolvedBirthDate) {
      const extractedBirthDate = extractBirthDateFromNationalId(normalizedNationalId);
      if (!extractedBirthDate) {
        setClubMemberFormError('تعذر استخراج تاريخ الميلاد من الرقم القومي. أدخل التاريخ يدويًا.');
        return;
      }

      resolvedBirthDate = extractedBirthDate;
      setClubMemberForm((current) => ({ ...current, birthDate: extractedBirthDate }));
    }

    if (roleNeedsGovernorate(clubMemberForm.role) && !clubMemberForm.governorateId) {
      setClubMemberFormError('المحافظة مطلوبة لهذا الدور.');
      return;
    }

    if (roleNeedsCommittee(clubMemberForm.role) && !clubMemberForm.committeeId) {
      setClubMemberFormError('اللجنة مطلوبة لهذا الدور.');
      return;
    }

    try {
      await createMember({
        ...clubMemberForm,
        fullName: clubMemberForm.fullName.trim(),
        email: clubMemberForm.email.trim().toLowerCase(),
        nationalId: normalizedNationalId,
        birthDate: resolvedBirthDate,
        governorateId: roleNeedsGovernorate(clubMemberForm.role) ? clubMemberForm.governorateId : '',
        committeeId: roleNeedsCommittee(clubMemberForm.role) ? clubMemberForm.committeeId : ''
      });

      setCreateClubMemberOpen(false);
      setClubMemberForm({ ...emptyMember, role: allowedClubCreateRoles[0] ?? 'CommitteeMember' });
      setClubMemberFormError('');
    } catch (error) {
      setClubMemberFormError(error instanceof Error ? error.message : 'تعذر إنشاء عضو النادي الطلابي.');
    }
  };

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.studentclubs.eyebrow}
        title={pageTitles.studentclubs.title}
        description={pageTitles.studentclubs.description}
        actions={canCreateClub || canCreateClubMember ? (
          <div className="flex flex-wrap gap-2">
            {canCreateClub && (
              <Button onClick={openCreateClubModal}>
                <span className="inline-flex items-center gap-2"><FiPlus /> إنشاء نادي طلابي</span>
              </Button>
            )}
            {canCreateClubMember && (
              <Button variant="secondary" onClick={openCreateClubMemberModal}>
                <span className="inline-flex items-center gap-2"><FiUserPlus /> إنشاء عضو نادي</span>
              </Button>
            )}
          </div>
        ) : undefined}
      />

      <div className="grid gap-4 sm:grid-cols-3">
        <StatCard label="منسق محافظة Club" value={roleStats.governorCoordinators} accent="brand" />
        <StatCard label="منسق لجنة Club" value={roleStats.committeeCoordinators} accent="amber" />
        <StatCard label="عضو لجنة Club" value={roleStats.committeeMembers} accent="success" />
      </div>

      {canManageClubJoinVisibility && (
        <Card title="فورم التقديم للـClub" subtitle="Club form visibility">
          <div className="space-y-4">
            <Field label="المحافظة">
              <Select
                value={selectedClubGovernorateId}
                onChange={(event) => setSelectedClubGovernorateId(event.target.value)}
                disabled={scopeLoading || user?.role === 'GovernorCommitteeCoordinator' || visibleGovernorates.length <= 1}
              >
                <option value="">اختر المحافظة</option>
                {visibleGovernorates.map((governorate) => <option key={governorate.governorateId} value={governorate.governorateId}>{governorate.name}</option>)}
              </Select>
            </Field>

            {clubCommittees.length === 0 ? (
              <EmptyState title="لا توجد لجان Club" description="أنشئ نادي طلابي أولاً أو اختر محافظة تحتوي على لجان Club." />
            ) : (
              <div className="space-y-3">
                {clubCommittees.map((committee) => (
                  <div key={committee.committeeId} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <p className="font-bold text-white">{committee.name}</p>
                        <p className="mt-1 text-xs text-slate-300">{committee.isVisibleInJoinForm ? 'الفورم مفتوح على هذا النادي' : 'الفورم مغلق على هذا النادي'}</p>
                      </div>
                      <Button
                        variant={committee.isVisibleInJoinForm ? 'ghost' : 'secondary'}
                        onClick={() => void toggleClubFormVisibility(committee)}
                        disabled={updatingClubCommitteeVisibilityId === committee.committeeId}
                      >
                        <span className="inline-flex items-center gap-2">
                          {updatingClubCommitteeVisibilityId === committee.committeeId
                            ? 'جارٍ التحديث...'
                            : committee.isVisibleInJoinForm
                              ? 'إغلاق الفورم'
                              : 'فتح الفورم'}
                        </span>
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </Card>
      )}

      <Card title="أعضاء النوادي الطلابية" subtitle="Club roster" actions={<Badge tone="neutral">{clubMembers.length} عضو</Badge>}>
        {clubMembers.length === 0 ? (
          <EmptyState title="لا توجد بيانات نوادي طلابية" description="سيظهر هنا أعضاء النوادي الطلابية المرتبطون بلجان Club فقط." />
        ) : (
          <div className="space-y-3">
            {clubMembers.map((member) => (
              <div key={member.memberId} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <p className="font-bold text-white">{member.fullName}</p>
                    <p className="text-sm text-slate-400">{member.email}</p>
                  </div>
                  <Badge tone="brand">{clubRoleLabel(member.role)}</Badge>
                </div>
                <div className="mt-3 text-xs text-slate-300">
                  <p>{member.governorName ? `المحافظة: ${member.governorName}` : 'بدون محافظة'}</p>
                  <p>{member.committeeName ? `اللجنة: ${member.committeeName}` : 'بدون لجنة'}</p>
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      <Modal
        open={createClubOpen}
        onClose={() => { setCreateClubOpen(false); setClubFormError(''); }}
        title="إنشاء نادي طلابي"
        subtitle="Club creation"
        footer={<><Button variant="ghost" onClick={() => { setCreateClubOpen(false); setClubFormError(''); }}>إلغاء</Button><Button type="submit" form="club-create-form"><span className="inline-flex items-center gap-2"><FiSave /> إنشاء</span></Button></>}
      >
        <form id="club-create-form" className="space-y-4" onSubmit={(event) => void submitCreateClub(event)}>
          {clubFormError && (
            <div className="rounded-2xl border border-rose-400/20 bg-rose-400/10 px-4 py-3 text-sm text-rose-200">{clubFormError}</div>
          )}
          <Field label="المحافظة">
            <Select
              value={clubGovernorateId}
              onChange={(event) => setClubGovernorateId(event.target.value)}
              disabled={scopeLoading || (user?.role === 'GovernorCoordinator' && visibleGovernorates.length <= 1)}
            >
              <option value="">اختر المحافظة</option>
              {visibleGovernorates.map((governorate) => <option key={governorate.governorateId} value={governorate.governorateId}>{governorate.name}</option>)}
            </Select>
          </Field>
          <Field label="اسم النادي الطلابي">
            <Input value={clubForm.name} onChange={(event) => setClubForm((current) => ({ ...current, name: event.target.value }))} placeholder="مثال: نادي علوم الحاسب" />
            <p className="mt-2 text-xs text-slate-400">إذا لم تبدأ التسمية بكلمة نادي، سيتم إضافتها تلقائيًا لحفظ تصنيف Club.</p>
          </Field>
        </form>
      </Modal>

      <Modal
        open={createClubMemberOpen}
        onClose={() => { setCreateClubMemberOpen(false); setClubMemberFormError(''); }}
        title="إنشاء عضو نادي"
        subtitle="Club member creation"
        footer={<><Button variant="ghost" onClick={() => { setCreateClubMemberOpen(false); setClubMemberFormError(''); }}>إلغاء</Button><Button type="submit" form="club-member-create-form"><span className="inline-flex items-center gap-2"><FiSave /> إنشاء</span></Button></>}
      >
        <form id="club-member-create-form" className="grid gap-4 md:grid-cols-2" onSubmit={(event) => void submitCreateClubMember(event)}>
          {clubMemberFormError && (
            <div className="md:col-span-2 rounded-2xl border border-rose-400/20 bg-rose-400/10 px-4 py-3 text-sm text-rose-200">{clubMemberFormError}</div>
          )}
          <Field label="الاسم رباعي" hint="مثال: محمد علي محمود أحمد"><Input value={clubMemberForm.fullName} onChange={(event) => setClubMemberForm((current) => ({ ...current, fullName: event.target.value }))} placeholder="الاسم الكامل" /></Field>
          <Field label="البريد الإلكتروني" hint="سيُستخدم لتسجيل الدخول"><Input value={clubMemberForm.email} onChange={(event) => setClubMemberForm((current) => ({ ...current, email: event.target.value }))} type="email" placeholder="example@domain.com" /></Field>
          <Field label="الرقم القومي" hint="يُستخرج منه تاريخ الميلاد تلقائياً"><Input value={clubMemberForm.nationalId} onChange={(event) => {
            const newId = event.target.value;
            setClubMemberForm((current) => {
              const updated = { ...current, nationalId: newId };
              const extractedDate = extractBirthDateFromNationalId(newId);
              if (extractedDate) {
                updated.birthDate = extractedDate;
              }
              return updated;
            });
          }} inputMode="numeric" maxLength={14} placeholder="14 رقمًا" /></Field>
          <Field label="تاريخ الميلاد" hint={clubMemberForm.birthDate ? `تُم استخراجه من الرقم القومي: ${clubMemberForm.birthDate.replace(/-/g, '/')}` : 'يملأ تلقائياً من الرقم القومي'}><Input value={clubMemberForm.birthDate} onChange={(event) => setClubMemberForm((current) => ({ ...current, birthDate: event.target.value }))} type="date" disabled={clubMemberForm.nationalId.length === 14} /></Field>

          <Field label="الدور" className="md:col-span-2">
            <Select value={clubMemberForm.role} onChange={(event) => setClubMemberForm((current) => ({ ...current, role: event.target.value as Role }))} disabled={allowedClubCreateRoles.length <= 1}>
              {allowedClubCreateRoles.map((role) => <option key={role} value={role}>{clubRoleLabel(role)}</option>)}
            </Select>
          </Field>

          {roleNeedsGovernorate(clubMemberForm.role) && (
            <Field label="المحافظة">
              <Select
                value={clubMemberForm.governorateId}
                onChange={(event) => setClubMemberForm((current) => ({ ...current, governorateId: event.target.value, committeeId: '' }))}
                disabled={scopeLoading || ((user?.role === 'GovernorCoordinator' || user?.role === 'GovernorCommitteeCoordinator') && visibleGovernorates.length <= 1)}
              >
                <option value="">اختر المحافظة</option>
                {visibleGovernorates.map((governorate) => <option key={governorate.governorateId} value={governorate.governorateId}>{governorate.name}</option>)}
              </Select>
            </Field>
          )}

          {roleNeedsCommittee(clubMemberForm.role) && (
            <Field label="اللجنة">
              <Select
                value={clubMemberForm.committeeId}
                onChange={(event) => setClubMemberForm((current) => ({ ...current, committeeId: event.target.value }))}
                disabled={!clubMemberForm.governorateId || scopeLoading || (user?.role === 'GovernorCommitteeCoordinator' && committees.length <= 1)}
              >
                <option value="">اختر لجنة Club</option>
                {committees.map((committee) => <option key={committee.committeeId} value={committee.committeeId}>{committee.name}</option>)}
              </Select>
            </Field>
          )}
        </form>
      </Modal>
    </div>
  );
}

function ImportantContactsPage() {
  const { importantContacts, createImportantContact, deleteImportantContact, loading, search } = useApp();
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState<ImportantContactCreateState>(emptyImportantContact);
  const [formError, setFormError] = useState('');

  const filtered = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    if (!normalized) {
      return importantContacts;
    }

    return importantContacts.filter((item) => [
      item.fullName,
      item.phoneNumber,
      item.positionTitle,
      item.domain
    ].join(' ').toLowerCase().includes(normalized));
  }, [importantContacts, search]);

  const isLoading = loading && importantContacts.length === 0;

  const removeContact = async (item: ImportantContactItem) => {
    if (!window.confirm(`هل تريد حذف رقم ${item.fullName}؟`)) {
      return;
    }

    await deleteImportantContact(item.id);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError('');

    if (!form.fullName.trim() || !hasAtLeastTwoNameParts(form.fullName)) {
      setFormError('يرجى إدخال الاسم الثنائي بشكل صحيح.');
      return;
    }

    if (!form.phoneNumber.trim() || form.phoneNumber.trim().length < 8) {
      setFormError('يرجى إدخال رقم هاتف صحيح.');
      return;
    }

    if (!form.positionTitle.trim()) {
      setFormError('يرجى إدخال المنصب.');
      return;
    }

    if (!form.domain.trim()) {
      setFormError('يرجى اختيار المجال.');
      return;
    }

    await createImportantContact(form);
    setForm(emptyImportantContact);
    setCreateOpen(false);
    setPage(1);
  };

  const columns: TableColumn<ImportantContactItem>[] = [
    {
      header: 'الاسم',
      render: (row) => (
        <div>
          <p className="font-bold text-white">{row.fullName}</p>
          <p className="text-xs text-slate-400">{row.positionTitle}</p>
        </div>
      )
    },
    {
      header: 'المجال',
      render: (row) => <Badge tone="brand">{row.domain}</Badge>
    },
    {
      header: 'الهاتف',
      render: (row) => <span className="text-sm text-slate-200">{row.phoneNumber}</span>
    },
    {
      header: 'أضيف في',
      render: (row) => <span className="text-xs text-slate-400">{formatDate(row.createdAtUtc)}</span>
    },
    {
      header: 'إجراءات',
      render: (row) => (
        <Button variant="ghost" onClick={() => void removeContact(row)}>
          <span className="inline-flex items-center gap-2"><FiTrash2 /> حذف</span>
        </Button>
      )
    }
  ];

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.importantcontacts.eyebrow}
        title={pageTitles.importantcontacts.title}
        description={pageTitles.importantcontacts.description}
        actions={<Button onClick={() => setCreateOpen(true)}><span className="inline-flex items-center gap-2"><FiPlus /> إضافة رقم</span></Button>}
      />

      <Card title="قائمة الشخصيات الهامة" subtitle="Key contacts">
        {isLoading ? (
          <EmptyState title="جاري التحميل" description="يتم الآن تحميل أرقام الشخصيات الهامة." />
        ) : (
          <PagedTable
            rows={filtered}
            columns={columns}
            rowKey={(row) => row.id}
            page={page}
            pageSize={6}
            onPageChange={setPage}
            search={search}
            emptyTitle="لا توجد بيانات"
            emptyDescription="قم بإضافة أول رقم شخصية هامة ليظهر هنا."
          />
        )}
      </Card>

      <Modal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        title="إضافة شخصية هامة"
        subtitle="Key contact form"
        footer={<><Button variant="ghost" onClick={() => setCreateOpen(false)}>إلغاء</Button><Button type="submit" form="important-contact-form"><span className="inline-flex items-center gap-2"><FiSave /> حفظ</span></Button></>}
      >
        <form id="important-contact-form" className="space-y-4" onSubmit={(event) => void submit(event)}>
          {formError && (
            <div className="rounded-2xl border border-rose-400/30 bg-rose-400/10 px-4 py-3 text-sm text-rose-100">
              {formError}
            </div>
          )}
          <Field label="الاسم الثنائي">
            <Input value={form.fullName} onChange={(event) => setForm((current) => ({ ...current, fullName: event.target.value }))} placeholder="مثال: أحمد علي" />
          </Field>
          <Field label="رقم الهاتف">
            <Input value={form.phoneNumber} onChange={(event) => setForm((current) => ({ ...current, phoneNumber: event.target.value }))} placeholder="01xxxxxxxxx" type="tel" />
          </Field>
          <Field label="المنصب">
            <Input value={form.positionTitle} onChange={(event) => setForm((current) => ({ ...current, positionTitle: event.target.value }))} placeholder="مثال: مدير شراكات" />
          </Field>
          <Field label="المجال">
            <Select value={form.domain} onChange={(event) => setForm((current) => ({ ...current, domain: event.target.value }))}>
              <option value="">اختر المجال</option>
              {importantContactDomains.map((domain) => <option key={domain} value={domain}>{domain}</option>)}
            </Select>
          </Field>
        </form>
      </Modal>
    </div>
  );
}

function SuggestionsPage() {
  const { addActivity } = useApp();
  const [suggestions, setSuggestions] = useState<SuggestionItem[]>([]);
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState<SuggestionFormState>(emptySuggestion);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadSuggestions = async () => {
      setLoading(true);
      try {
        const result = await getSuggestions();
        setSuggestions(result);
      } catch {
        setSuggestions([]);
      } finally {
        setLoading(false);
      }
    };

    void loadSuggestions();
  }, []);

  const saveSuggestion = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!form.title.trim() || !form.description.trim()) {
      window.alert('يرجى إدخال عنوان ووصف المقترح.');
      return;
    }

    try {
      await createSuggestion(form);
      setForm(emptySuggestion);
      setCreateOpen(false);
      const result = await getSuggestions();
      setSuggestions(result);
      if (addActivity) {
        addActivity('تقديم مقترح', `تم تقديم مقترح: ${form.title}`, 'success');
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'تعذر تقديم المقترح. حاول مرة أخرى.';
      window.alert(message);
    }
  };

  const handleVote = async (suggestionId: string, isAcceptance: boolean) => {
    try {
      const updated = await voteSuggestion(suggestionId, isAcceptance);
      setSuggestions((current) =>
        current.map((item) => (item.id === suggestionId ? updated : item))
      );
    } catch {
      return;
    }
  };

  const columns: TableColumn<SuggestionItem>[] = [
    {
      header: 'الاقتراح',
      render: (row) => (
        <div>
          <p className="font-bold text-white">{row.title}</p>
          <p className="text-xs text-slate-400">{row.description.slice(0, 60)}...</p>
        </div>
      )
    },
    {
      header: 'المقدم',
      render: (row) => (
        <div className="text-sm">
          <p className="text-white">{row.createdByMemberName}</p>
          <p className="text-xs text-slate-400">{row.createdByMemberRole}</p>
        </div>
      )
    },
    {
      header: 'الحالة',
      render: (row) => (
        <Badge
          tone={
            row.status === 'Accepted'
              ? 'success'
              : row.status === 'Rejected'
              ? 'danger'
              : 'neutral'
          }
        >
          {row.status === 'Open' ? 'مفتوح' : row.status === 'Accepted' ? 'مقبول' : 'مرفوض'}
        </Badge>
      )
    },
    {
      header: 'التصويت',
      render: (row) => (
        <div className="flex items-center gap-2">
          <span className="text-xs font-bold text-emerald-300">{row.acceptanceCount}</span>
          <span className="text-xs text-slate-400">/</span>
          <span className="text-xs font-bold text-rose-300">{row.rejectionCount}</span>
        </div>
      )
    },
    {
      header: 'تصويتك',
      render: (row) => {
        const userVoted =
          row.currentUserVote !== null
            ? row.currentUserVote
              ? 'accept'
              : 'reject'
            : null;

        return (
          <div className="flex items-center gap-2">
            <button
              onClick={() => void handleVote(row.id, true)}
              className={`rounded-lg px-2 py-1 transition ${
                userVoted === 'accept'
                  ? 'bg-emerald-400/30 text-emerald-200'
                  : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
              }`}
            >
              <FiThumbsUp className="h-4 w-4" />
            </button>
            <button
              onClick={() => void handleVote(row.id, false)}
              className={`rounded-lg px-2 py-1 transition ${
                userVoted === 'reject'
                  ? 'bg-rose-400/30 text-rose-200'
                  : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
              }`}
            >
              <FiThumbsDown className="h-4 w-4" />
            </button>
          </div>
        );
      }
    }
  ];

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.suggestions.eyebrow}
        title={pageTitles.suggestions.title}
        description={pageTitles.suggestions.description}
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <span className="inline-flex items-center gap-2">
              <FiPlus /> اقتراح جديد
            </span>
          </Button>
        }
      />

      <Card title="قائمة المقترحات" subtitle="Suggestions">
        {loading ? (
          <EmptyState title="جاري التحميل" description="يتم تحميل المقترحات..." />
        ) : suggestions.length === 0 ? (
          <EmptyState
            title="لا توجد مقترحات"
            description="ابدأ بتقديم أول مقتراح لتطوير أداء المؤسسة."
          />
        ) : (
          <PagedTable
            rows={suggestions}
            columns={columns}
            rowKey={(row) => row.id}
            page={page}
            pageSize={6}
            onPageChange={setPage}
            search=""
            emptyTitle="لا توجد مقترحات"
            emptyDescription="لا توجد مقترحات مطابقة للبحث."
          />
        )}
      </Card>

      <Modal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        title="تقديم مقتراح جديد"
        subtitle="Suggestion form"
      >
        <form
          id="suggestion-form"
          className="space-y-4"
          onSubmit={(event) => void saveSuggestion(event)}
        >
          <Field label="عنوان المقترح">
            <Input
              value={form.title}
              onChange={(event) =>
                setForm((current) => ({ ...current, title: event.target.value }))
              }
              placeholder="ملخص الفكرة أو الاقتراح"
              required
            />
          </Field>
          <Field label="الوصف التفصيلي">
            <Textarea
              value={form.description}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  description: event.target.value
                }))
              }
              rows={6}
              placeholder="شرح مفصل للمقتراح والفوائد المتوقعة"
              required
            />
          </Field>
          <div className="mt-6 flex flex-wrap items-center justify-end gap-3">
            <Button variant="ghost" onClick={() => setCreateOpen(false)}>
              إلغاء
            </Button>
            <Button type="submit">
              <span className="inline-flex items-center gap-2">
                <FiSave /> تقديم
              </span>
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}

function ReportsPage() {
  const { dashboard, members, tasks, complaints, activityLogs } = useApp();

  const toCsvCell = (value: string) => {
    const normalized = value.replace(/\r?\n/g, ' ').trim();
    return /[",\n]/.test(normalized)
      ? `"${normalized.replace(/"/g, '""')}"`
      : normalized;
  };

  const downloadSheet = (filename: string, rows: string[][]) => {
    const csv = rows
      .map((row) => row.map((cell) => toCsvCell(cell)).join(','))
      .join('\r\n');

    const blob = new Blob(['\ufeff', csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.reports.eyebrow}
        title={pageTitles.reports.title}
        description={pageTitles.reports.description}
      />

      <div className="grid gap-4 md:grid-cols-3">
        <StatCard label="الأعضاء" value={members.length} hint="قائمة الأعضاء الحالية" />
        <StatCard label="المهام" value={tasks.length} hint="مهام المستخدمين" accent="sky" />
        <StatCard label="الشكاوى" value={complaints.length} hint="الطلبات المرسلة" accent="amber" />
      </div>

      <Card title="التصدير" subtitle="Export tools" actions={<Button variant="secondary" onClick={() => window.print()}><span className="inline-flex items-center gap-2"><FiPrinter /> طباعة / PDF</span></Button>}>
        <div className="flex flex-wrap gap-3">
          <Button onClick={() => downloadSheet('الأعضاء.csv', [['الاسم', 'البريد', 'الرقم القومي', 'تاريخ الميلاد', 'الدور', 'النقاط'], ...members.map((member) => [member.fullName, member.email, member.nationalId ?? '', member.birthDate ? formatDateOnly(member.birthDate) : '', roleLabel(member.role), String(member.points)])])}>
            <span className="inline-flex items-center gap-2"><FiDownload /> تصدير الأعضاء</span>
          </Button>
          <Button variant="secondary" onClick={() => downloadSheet('المهام.csv', [['العنوان', 'الوصف', 'الحالة'], ...tasks.map((task) => [task.title, task.description ?? '', task.isCompleted ? 'مكتملة' : 'قيد التنفيذ'])])}>
            <span className="inline-flex items-center gap-2"><FiDownload /> تصدير المهام</span>
          </Button>
          <Button variant="secondary" onClick={() => downloadSheet('الشكاوى.csv', [['الموضوع', 'مقدم الشكوى', 'الرد الإداري', 'الحالة'], ...complaints.map((item) => [item.subject, item.memberName, item.adminReply ?? '', statusLabel(item.status)])])}>
            <span className="inline-flex items-center gap-2"><FiDownload /> تصدير الشكاوى</span>
          </Button>
        </div>
      </Card>

      <Card title="سجل النشاط" subtitle="Log overview">
        <div className="space-y-3">
          {activityLogs.length === 0 ? (
            <EmptyState title="لا يوجد سجل" description="سيظهر سجل العمليات هنا تلقائيًا أثناء الاستخدام." />
          ) : activityLogs.map((log) => (
            <div key={log.id} className="flex items-start justify-between gap-4 rounded-2xl border border-white/10 bg-white/5 p-4">
              <div>
                <p className="font-bold text-white">{log.title}</p>
                <p className="mt-1 text-sm leading-7 text-slate-300">{log.description}</p>
              </div>
              <Badge tone={log.tone === 'warning' ? 'warning' : log.tone === 'success' ? 'success' : 'neutral'}>{formatDate(log.createdAtUtc)}</Badge>
            </div>
          ))}
        </div>
      </Card>

      <Card title="مؤشرات آنية" subtitle="Overview snapshot">
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <StatCard label="إجمالي الأعضاء" value={dashboard?.totalMembers ?? members.length} />
          <StatCard label="أعلى نقاط" value={dashboard?.topMembers[0]?.points ?? 0} accent="success" />
          <StatCard label="شكاوى مفتوحة" value={dashboard?.openComplaints ?? 0} accent="rose" />
          <StatCard label="مركزك الحالي" value={dashboard?.currentMemberName ?? '—'} accent="amber" />
        </div>
      </Card>
    </div>
  );
}

function AuditLogsPage() {
  const { members } = useApp();
  const [filters, setFilters] = useState<AuditLogFilters>({ page: 1, pageSize: 10 });
  const [data, setData] = useState<{ items: AuditLogItem[]; totalCount: number; page: number; pageSize: number }>({ items: [], totalCount: 0, page: 1, pageSize: 10 });
  const [loading, setLoading] = useState(false);
  const [selectedLogId, setSelectedLogId] = useState('');

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      setLoading(true);
      try {
        const result = await getAuditLogs(filters);
        if (!cancelled) {
          setData(result);
          setSelectedLogId((current) => current || result.items[0]?.id || '');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, [filters]);

  const selectedLog = data.items.find((item) => item.id === selectedLogId) ?? data.items[0] ?? null;
  const totalPages = Math.max(1, Math.ceil(data.totalCount / data.pageSize));

  const setFilter = (key: keyof AuditLogFilters, value: string | number) => {
    setFilters((current) => ({ ...current, [key]: value || undefined, page: 1 }));
  };

  return (
    <div className="space-y-6">
      <SectionTitle
        eyebrow={pageTitles.auditlogs.eyebrow}
        title={pageTitles.auditlogs.title}
        description={pageTitles.auditlogs.description}
        actions={<Button variant="secondary" onClick={() => setFilters((current) => ({ ...current }))}><span className="inline-flex items-center gap-2"><FiActivity /> تحديث</span></Button>}
      />

      <Card title="فلاتر البحث" subtitle="Filters">
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-6">
          <Field label="المستخدم">
            <Select value={filters.userId ?? ''} onChange={(event) => setFilter('userId', event.target.value)}>
              <option value="">الكل</option>
              {members.map((member) => <option key={member.memberId} value={member.memberId}>{member.fullName}</option>)}
            </Select>
          </Field>
          <Field label="العملية">
            <Select value={filters.actionType ?? ''} onChange={(event) => setFilter('actionType', event.target.value)}>
              <option value="">الكل</option>
              {['Create', 'Update', 'Delete', 'Assign', 'Login', 'Escalate', 'Commented'].map((action) => <option key={action} value={action}>{action}</option>)}
            </Select>
          </Field>
          <Field label="الكيان">
            <Select value={filters.entityName ?? ''} onChange={(event) => setFilter('entityName', event.target.value)}>
              <option value="">الكل</option>
              {['User', 'Task', 'Complaint'].map((entity) => <option key={entity} value={entity}>{entity}</option>)}
            </Select>
          </Field>
          <Field label="من تاريخ"><Input value={filters.fromUtc ?? ''} onChange={(event) => setFilter('fromUtc', event.target.value)} type="date" /></Field>
          <Field label="إلى تاريخ"><Input value={filters.toUtc ?? ''} onChange={(event) => setFilter('toUtc', event.target.value)} type="date" /></Field>
          <Field label="بحث"><Input value={filters.search ?? ''} onChange={(event) => setFilter('search', event.target.value)} placeholder="اسم / عملية / كيان" /></Field>
        </div>
      </Card>

      <div className="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
        <Card title="السجلات" subtitle="Log table">
          <div className="space-y-3 sm:hidden">
            {loading ? (
              <EmptyState title="جاري التحميل" description="يتم الآن جلب السجلات." />
            ) : data.items.length === 0 ? (
              <EmptyState title="لا توجد سجلات" description="لا توجد سجلات مطابقة لنتائج البحث الحالية." />
            ) : (
              data.items.map((log) => (
                <div key={log.id} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <Badge tone={log.actionType === 'Delete' ? 'danger' : log.actionType === 'Update' ? 'warning' : 'brand'}>{log.actionType}</Badge>
                    <span className="text-xs text-slate-400">{formatDate(log.timestampUtc)}</span>
                  </div>
                  <div className="mt-3 space-y-1 text-sm text-slate-300">
                    <p className="text-slate-100">{log.userName}</p>
                    <p>الكيان: {log.entityName}</p>
                    <p className="break-all">المرجع: {log.entityId ?? '—'}</p>
                    <p className="break-all">IP: {log.ipAddress ?? '—'}</p>
                  </div>
                  <Button variant="ghost" className="mt-3 w-full" onClick={() => setSelectedLogId(log.id)}>
                    عرض التفاصيل
                  </Button>
                </div>
              ))
            )}
            <div className="flex flex-col gap-2 pt-2">
              <p className="text-xs text-slate-400">الصفحة {data.page} من {totalPages} - {data.totalCount} سجل</p>
              <div className="flex gap-2">
                <Button variant="secondary" disabled={data.page <= 1} onClick={() => setFilters((current) => ({ ...current, page: Math.max(1, (current.page ?? 1) - 1) }))}>السابق</Button>
                <Button variant="secondary" disabled={data.page >= totalPages} onClick={() => setFilters((current) => ({ ...current, page: (current.page ?? 1) + 1 }))}>التالي</Button>
              </div>
            </div>
          </div>
          <div className="hidden overflow-hidden rounded-3xl border border-white/10 bg-slate-950/60 sm:block">
            <div className="overflow-x-auto">
              <table className="min-w-full text-right text-sm">
                <thead className="bg-white/5 text-slate-300">
                  <tr>
                    <th className="px-4 py-4 font-semibold">الوقت</th>
                    <th className="px-4 py-4 font-semibold">المستخدم</th>
                    <th className="px-4 py-4 font-semibold">العملية</th>
                    <th className="px-4 py-4 font-semibold">الكيان</th>
                    <th className="px-4 py-4 font-semibold">المرجع</th>
                    <th className="px-4 py-4 font-semibold">IP</th>
                    <th className="px-4 py-4 font-semibold">تفاصيل</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr><td className="px-4 py-6 text-slate-400" colSpan={7}>جاري التحميل...</td></tr>
                  ) : data.items.length === 0 ? (
                    <tr><td className="px-4 py-6 text-slate-400" colSpan={7}>لا توجد سجلات مطابقة.</td></tr>
                  ) : data.items.map((log, index) => (
                    <tr key={log.id} className={index % 2 === 0 ? 'bg-white/[0.02]' : 'bg-white/[0.04]'}>
                      <td className="px-4 py-4 text-slate-200">{formatDate(log.timestampUtc)}</td>
                      <td className="px-4 py-4 text-slate-200">{log.userName}</td>
                      <td className="px-4 py-4"><Badge tone={log.actionType === 'Delete' ? 'danger' : log.actionType === 'Update' ? 'warning' : 'brand'}>{log.actionType}</Badge></td>
                      <td className="px-4 py-4 text-slate-200">{log.entityName}</td>
                      <td className="px-4 py-4 text-slate-200">{log.entityId ?? '—'}</td>
                      <td className="px-4 py-4 text-slate-200">{log.ipAddress ?? '—'}</td>
                      <td className="px-4 py-4"><Button variant="ghost" onClick={() => setSelectedLogId(log.id)}>عرض</Button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="flex flex-col gap-3 border-t border-white/10 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
              <p className="text-sm text-slate-400">الصفحة {data.page} من {totalPages} - {data.totalCount} سجل</p>
              <div className="flex gap-2">
                <Button variant="secondary" disabled={data.page <= 1} onClick={() => setFilters((current) => ({ ...current, page: Math.max(1, (current.page ?? 1) - 1) }))}>السابق</Button>
                <Button variant="secondary" disabled={data.page >= totalPages} onClick={() => setFilters((current) => ({ ...current, page: (current.page ?? 1) + 1 }))}>التالي</Button>
              </div>
            </div>
          </div>
        </Card>

        <Card title="تفاصيل الفرق" subtitle="Old vs new">
          {selectedLog ? (
            <div className="space-y-4">
              <div className="rounded-3xl border border-white/10 bg-white/5 p-4">
                <p className="text-lg font-bold text-white">{selectedLog.actionType} - {selectedLog.entityName}</p>
                <p className="mt-1 text-sm text-slate-400">{selectedLog.userName}</p>
                <p className="mt-1 text-sm text-slate-400">{formatDate(selectedLog.timestampUtc)}</p>
              </div>
              {renderAuditDiff(selectedLog)}
              <div className="rounded-3xl border border-white/10 bg-white/5 p-4 text-sm text-slate-300">
                <p>EntityId: {selectedLog.entityId ?? '—'}</p>
                <p className="mt-1">IP: {selectedLog.ipAddress ?? '—'}</p>
              </div>
            </div>
          ) : (
            <EmptyState title="اختر سجلًا" description="حدد أي عملية من الجدول لعرض التغييرات القديمة والجديدة بشكل واضح." />
          )}
        </Card>
      </div>
    </div>
  );
}

function ProfilePage() {
  const { user, dashboard, leaderboard, activityLogs } = useApp();

  return (
    <div className="space-y-6">
      <SectionTitle eyebrow={pageTitles.profile.eyebrow} title={pageTitles.profile.title} description={pageTitles.profile.description} />

      <div className="mx-auto w-full max-w-3xl">
        <PasswordChangeView />
      </div>

      <div className="grid gap-6 xl:grid-cols-[0.8fr_1.2fr]">
        <Card title="بيانات الحساب" subtitle="Profile">
          <div className="space-y-4">
            <div className="rounded-3xl border border-white/10 bg-white/5 p-5">
              <p className="text-xs text-slate-400">الاسم</p>
              <p className="mt-1 text-xl font-bold text-white">{user?.fullName}</p>
              <p className="mt-1 break-all text-sm text-slate-400">{user?.email}</p>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <StatCard label="الدور" value={user?.role ?? '—'} accent="brand" />
              <StatCard label="النقاط" value={user?.points ?? 0} accent="success" />
            </div>
          </div>
        </Card>

        <div className="space-y-6">
          <Card
            title="أفضل المتصدرين"
            subtitle="Top 10"
            actions={<Badge tone="neutral">{leaderboard.length} عضو</Badge>}
          >
            <div className="space-y-3">
              {leaderboard.length === 0 ? (
                <EmptyState title="لا توجد بيانات بعد" description="ستظهر قائمة المتصدرين هنا عند توفر النقاط." />
              ) : leaderboard.map((entry) => (
                <div key={entry.memberId} className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-white/10 bg-white/5 px-4 py-3">
                  <div>
                    <p className="font-bold text-white">#{entry.rank} {entry.fullName}</p>
                    <p className="text-sm text-slate-400">{roleLabel(entry.role)}</p>
                  </div>
                  <Badge tone="success">{entry.points} نقطة</Badge>
                </div>
              ))}
            </div>
          </Card>

          <Card title="الصلاحيات" subtitle="Permissions">
            <div className="max-h-48 overflow-auto rounded-2xl border border-white/10 bg-white/5 p-3 scrollbar-thin">
              <ul className="space-y-2 text-sm text-slate-200">
                {user?.permissions.map((permission) => (
                  <li key={permission} className="flex items-center gap-2 rounded-xl bg-slate-950/45 px-3 py-2">
                    <span className="h-2 w-2 rounded-full bg-brand-300" />
                    <span className="truncate">{permission}</span>
                  </li>
                ))}
              </ul>
            </div>
          </Card>

          <Card title="ملخص سريع" subtitle="Snapshot">
            <div className="grid gap-4 sm:grid-cols-2">
              <StatCard label="الأعضاء" value={dashboard?.totalMembers ?? 0} />
              <StatCard label="المفتوحة" value={dashboard?.openComplaints ?? 0} accent="rose" />
            </div>
          </Card>

          <Card title="أحدث الأنشطة" subtitle="Recent activity">
            <div className="space-y-3">
              {activityLogs.slice(0, 4).map((item) => (
                <div key={item.id} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  <p className="font-bold text-white">{item.title}</p>
                  <p className="mt-1 text-sm text-slate-300">{item.description}</p>
                </div>
              ))}
              {activityLogs.length === 0 && <EmptyState title="لا يوجد نشاط" description="النشاطات الحديثة ستظهر هنا بعد أول عملية." />}
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}

export default function App() {
  const { isAuthenticated, section, clearError, navigation, setSection } = useApp();
  const [publicRoute, setPublicRoute] = useState<PublicRoute>(() => resolvePublicRoute());
  const [privateRouteReady, setPrivateRouteReady] = useState(false);

  useEffect(() => {
    clearError();
  }, [clearError]);

  useEffect(() => {
    if (isAuthenticated) {
      return;
    }

    const onLocationChange = () => {
      setPublicRoute(resolvePublicRoute());
    };

    window.addEventListener('popstate', onLocationChange);
    window.addEventListener('hashchange', onLocationChange);

    return () => {
      window.removeEventListener('popstate', onLocationChange);
      window.removeEventListener('hashchange', onLocationChange);
    };
  }, [isAuthenticated]);

  useEffect(() => {
    if (isAuthenticated) {
      return;
    }

    const pathname = normalizePathname(window.location.pathname);
    if (pathname === '/' || pathname === '/login' || pathname === '/join') {
      return;
    }

    window.history.replaceState(null, '', '/login');
    setPublicRoute('login');
  }, [isAuthenticated]);

  useEffect(() => {
    if (!isAuthenticated) {
      setPrivateRouteReady(false);
      return;
    }

    const syncSectionFromPath = () => {
      const routedSection = resolvePrivateSection(window.location.pathname);
      const allowed = navigation.some((item) => item.key === routedSection);
      const targetSection = allowed ? routedSection : 'overview';

      setSection(targetSection);
      setPrivateRouteReady(true);
    };

    syncSectionFromPath();
    window.addEventListener('popstate', syncSectionFromPath);

    return () => window.removeEventListener('popstate', syncSectionFromPath);
  }, [isAuthenticated, navigation, setSection]);

  useEffect(() => {
    if (!isAuthenticated || !privateRouteReady) {
      return;
    }

    const targetPath = sectionPathByKey[section] ?? '/dashbourd';
    const currentPath = normalizePathname(window.location.pathname);

    if (currentPath !== targetPath) {
      window.history.pushState(null, '', `${targetPath}${window.location.search}`);
    }
  }, [isAuthenticated, privateRouteReady, section]);

  const goToPublicRoute = (route: PublicRoute) => {
    const targetPath = route === 'join' ? '/join' : '/login';
    window.history.replaceState(null, '', `${targetPath}${window.location.search}`);
    setPublicRoute(route);
  };

  if (!isAuthenticated) {
    if (publicRoute === 'join') {
      return <JoinRequestView onBackToLogin={() => goToPublicRoute('login')} />;
    }

    return <LoginView onNavigateToJoin={() => goToPublicRoute('join')} />;
  }

  const content: Record<SectionKey, ReactElement> = {
    overview: <OverviewPage />,
    leaderboard: <LeaderboardPage />,
    news: <NewsPage />,
    joinrequests: <JoinRequestsPage />,
    members: <MembersPage />,
    studentclubs: <StudentClubsPage />,
    tasks: <TasksPage />,
    complaints: <ComplaintsPage />,
    auditlogs: <AuditLogsPage />,
    committees: <CommitteesPage />,
    importantcontacts: <ImportantContactsPage />,
    suggestions: <SuggestionsPage />,
    reports: <ReportsPage />,
    profile: <ProfilePage />
  };

  return (
    <AppShell>
      {content[section]}
    </AppShell>
  );
}

