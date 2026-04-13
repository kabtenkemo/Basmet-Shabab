import { useEffect, useMemo, useState, type FormEvent, type ReactElement } from 'react';
import { FiActivity, FiArrowLeft, FiClock, FiDownload, FiEdit3, FiPlus, FiPrinter, FiSave, FiSend, FiTrash2 } from 'react-icons/fi';
import * as XLSX from 'xlsx';
import { AppShell } from './components/AppShell';
import { Badge, Button, Card, EmptyState, Field, Input, Modal, PagedTable, Select, SectionTitle, StatCard, Textarea, type TableColumn } from './components/ui';
import { useApp } from './context/AppContext';
import { commentComplaint, createCommittee, escalateComplaint, getAuditLogs, getComplaint, getGovernorateCommittees, getGovernorates } from './api';
import type { AuditLogFilters, AuditLogItem, CommitteeCreateFormState, CommitteeOption, ComplaintCommentState, ComplaintDetail, ComplaintEscalateState, ComplaintFormState, ComplaintItem, ComplaintReviewState, GovernorateOption, MemberAdminItem, MemberCreateFormState, NewsCreateState, NewsItem, PointFormState, Role, SectionKey, TaskAudienceType, TaskFormState, TaskItem } from './types';

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

const pageTitles: Record<SectionKey, { eyebrow: string; title: string; description: string }> = {
  overview: {
    eyebrow: 'Dashboard',
    title: 'لوحة متابعة شاملة بالعربية وبتنظيم هرمي واضح',
    description: 'عرض سريع للأعضاء والمهام والشكاوى والمتصدرين مع واجهة RTL جاهزة للعرض والاستخدام اليومي.'
  },
  news: {
    eyebrow: 'News',
    title: 'إعلانات وأخبار الكيان',
    description: 'رسائل رسمية من رئيس الكيان أو مساعد الرئيس، موجهة للجميع أو لفئات محددة.'
  },
  members: {
    eyebrow: 'User Management',
    title: 'إدارة الأعضاء والصلاحيات',
    description: 'إنشاء أعضاء جدد، تغيير الأدوار، منح الصلاحيات، وتعديل النقاط من شاشة واحدة.'
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

function formatDate(value: string | null | undefined) {
  if (!value) {
    return '—';
  }

  return new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function formatDateOnly(value: string | null | undefined) {
  if (!value) {
    return '—';
  }

  return new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium' }).format(new Date(`${value}T00:00:00`));
}

function roleLabel(role: string) {
  return roleLabels[role as Role] ?? role;
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

function LoginView() {
  const { loginUser, loading, error, clearError } = useApp();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  useEffect(() => {
    clearError();
  }, [clearError]);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await loginUser(email, password);
  };

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
            <h1 className="mt-4 max-w-3xl text-4xl font-black leading-tight text-white sm:text-5xl lg:text-6xl">
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
        <Card title={loginTitle()} subtitle="Authentication" className="w-full max-w-xl">
          <form className="space-y-4" onSubmit={submit}>
            <Field label="البريد الإلكتروني">
              <Input value={email} onChange={(event) => setEmail(event.target.value)} type="email" placeholder="president@basmet.local" />
            </Field>
            <Field label="كلمة المرور">
              <Input value={password} onChange={(event) => setPassword(event.target.value)} type="password" placeholder="••••••••" />
            </Field>

            {error && <div className="rounded-2xl border border-rose-400/20 bg-rose-400/10 px-4 py-3 text-sm text-rose-200">{error}</div>}

            <Button type="submit" className="w-full" disabled={loading}>
              {loading ? 'جاري تسجيل الدخول...' : 'دخول'}
            </Button>
          </form>

          <div className="mt-6 rounded-3xl border border-white/10 bg-white/5 p-4 text-sm leading-7 text-slate-300">
            التسجيل الخارجي مغلق. الحسابات تُنشأ من داخل النظام فقط.
          </div>
        </Card>
      </section>
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

  return (
    <Card title="تغيير كلمة المرور" subtitle="Mandatory security step" className="w-full">
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
        الحساب الحالي {user?.fullName ? `لـ ${user.fullName}` : ''} يستخدم كلمة مرور مؤقتة.
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
              <div key={entry.memberId} className="flex items-center justify-between rounded-2xl border border-white/10 bg-white/5 px-4 py-3">
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
                <div className="flex items-center justify-between gap-3">
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
                <div className="flex items-center justify-between gap-3">
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

function NewsPage() {
  const { news, members, canManageNews, createNewsItem } = useApp();
  const [createOpen, setCreateOpen] = useState(false);
  const [newsForm, setNewsForm] = useState<NewsCreateState>(emptyNews);

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
        {news.length === 0 ? (
          <EmptyState title="لا يوجد أخبار حالياً" description="عند نشر إعلان جديد من الإدارة سيظهر هنا." />
        ) : (
          <div className="space-y-3">
            {news.map((item: NewsItem) => (
              <div key={item.id} className="rounded-3xl border border-white/10 bg-white/5 p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="text-lg font-bold text-white">{item.title}</p>
                  <Badge tone="brand">{audienceLabel(item.audienceType)}</Badge>
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

function MembersPage() {
  const { members, search, createMember, changeRole, assignPermission, changePoints, canManageUsers, canCreateMembers, user } = useApp();
  const [createOpen, setCreateOpen] = useState(false);
  const [page, setPage] = useState(1);
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
    const normalized = search.trim().toLowerCase();
    return members.filter((member) => [member.fullName, member.email, member.role, member.permissions.join(' ')].join(' ').toLowerCase().includes(normalized));
  }, [members, search]);

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

    if (nameParts.length < 4) {
      setMemberFormError('الاسم رباعي مطلوب.');
      return;
    }

    if (normalizedNationalId.length !== 14 || /[^0-9]/.test(normalizedNationalId)) {
      setMemberFormError('الرقم القومي يجب أن يكون 14 رقمًا.');
      return;
    }

    if (!memberForm.birthDate) {
      setMemberFormError('تاريخ الميلاد مطلوب.');
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

    await createMember(memberForm);
    setCreateOpen(false);
    setMemberForm(emptyMember);
    setCommittees([]);
    setMemberFormError('');
  };

  const saveRole = async () => {
    if (!selectedMember) return;
    await changeRole(selectedMember.memberId, selectedRole);
  };

  const savePermission = async () => {
    if (!selectedMember || selectedPermissions.length === 0) return;

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
          <PagedTable
            rows={filtered}
            columns={columns}
            rowKey={(row) => row.memberId}
            page={page}
            pageSize={6}
            onPageChange={setPage}
            search={search}
            emptyTitle="لا يوجد أعضاء"
            emptyDescription="لن تظهر العناصر هنا إلا عند توفر بيانات الأعضاء من الـ API."
          />
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
                    {permissionOptions.map((permission) => (
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
              <Button className="w-full" variant="secondary" onClick={() => void savePermission()} disabled={!canManageUsers || selectedPermissions.length === 0}>إضافة الصلاحيات المحددة</Button>

              <Field label="تعديل النقاط">
                <Input value={pointForm.amount} onChange={(event) => setPointForm((current) => ({ ...current, amount: event.target.value }))} type="number" />
              </Field>
              <Field label="سبب التعديل">
                <Input value={pointForm.reason} onChange={(event) => setPointForm((current) => ({ ...current, reason: event.target.value }))} placeholder="مكافأة / خصم / نشاط" />
              </Field>
              <Button className="w-full" variant="secondary" onClick={() => void savePoints()} disabled={!canManageUsers}>تعديل النقاط</Button>
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
        subtitle="Default password: 123"
        footer={<><Button variant="ghost" onClick={() => { setCreateOpen(false); setMemberFormError(''); }}><span className="inline-flex items-center gap-2"><FiArrowLeft /> إلغاء</span></Button><Button type="submit" form="member-create-form"><span className="inline-flex items-center gap-2"><FiSave /> إنشاء</span></Button></>}
      >
        <form id="member-create-form" className="grid gap-4 md:grid-cols-2" onSubmit={(event) => void saveMember(event)}>
          {memberFormError && <div className="md:col-span-2 rounded-2xl border border-rose-400/20 bg-rose-400/10 px-4 py-3 text-sm text-rose-200">{memberFormError}</div>}
          <Field label="الاسم رباعي"><Input value={memberForm.fullName} onChange={(event) => setMemberForm((current) => ({ ...current, fullName: event.target.value }))} /></Field>
          <Field label="البريد الإلكتروني"><Input value={memberForm.email} onChange={(event) => setMemberForm((current) => ({ ...current, email: event.target.value }))} type="email" /></Field>
          <Field label="الرقم القومي"><Input value={memberForm.nationalId} onChange={(event) => setMemberForm((current) => ({ ...current, nationalId: event.target.value }))} inputMode="numeric" maxLength={14} placeholder="14 رقمًا" /></Field>
          <Field label="تاريخ الميلاد"><Input value={memberForm.birthDate} onChange={(event) => setMemberForm((current) => ({ ...current, birthDate: event.target.value }))} type="date" /></Field>
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
  const { tasks, members, search, createTaskItem, updateTaskItem, deleteTaskItem, user } = useApp();
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
    { header: 'الإجراءات', render: (row) => <div className="flex flex-wrap gap-2"><Button variant="ghost" onClick={() => { setEditing(row); setTaskForm({ title: row.title, description: row.description ?? '', dueDate: row.dueDate?.slice(0, 10) ?? '', audienceType: (row.audienceType as TaskAudienceType) ?? 'All', targetRoles: row.targetRoles as Role[], targetMemberIds: row.targetMemberIds, isCompleted: row.isCompleted }); setTaskOpen(true); }}><span className="inline-flex items-center gap-2"><FiEdit3 /> تعديل</span></Button><Button variant="danger" onClick={() => void deleteTaskItem(row.id)}><span className="inline-flex items-center gap-2"><FiTrash2 /> حذف</span></Button></div> }
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
  const { user } = useApp();
  const [governorates, setGovernorates] = useState<GovernorateOption[]>([]);
  const [selectedGovernorateId, setSelectedGovernorateId] = useState('');
  const [committees, setCommittees] = useState<CommitteeOption[]>([]);
  const [form, setForm] = useState<CommitteeCreateFormState>(emptyCommittee);

  const canManageCommitteeCatalog = user?.role === 'President' || user?.role === 'VicePresident' || user?.role === 'GovernorCoordinator';
  const visibleGovernorates = useMemo(() => {
    if (user?.role === 'GovernorCoordinator' && user.governorName) {
      return governorates.filter((governorate) => governorate.name === user.governorName);
    }

    return governorates;
  }, [governorates, user?.governorName, user?.role]);

  useEffect(() => {
    let cancelled = false;

    const loadGovernorates = async () => {
      try {
        const result = await getGovernorates();
        if (!cancelled) {
          setGovernorates(result);
          const filteredGovernorates = user?.role === 'GovernorCoordinator' && user.governorName
            ? result.filter((governorate) => governorate.name === user.governorName)
            : result;
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
  }, []);

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
          setCommittees(result);
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
  }, [selectedGovernorateId]);

  const addCommittee = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selectedGovernorateId || !form.name.trim()) {
      return;
    }

    try {
      await createCommittee(selectedGovernorateId, form);
      setForm(emptyCommittee);
      setCommittees(await getGovernorateCommittees(selectedGovernorateId));
    } catch {
      return;
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
          {canManageCommitteeCatalog ? (
            <form className="space-y-4" onSubmit={(event) => void addCommittee(event)}>
              <Field label="المحافظة">
                <Select value={selectedGovernorateId} onChange={(event) => setSelectedGovernorateId(event.target.value)}>
                  <option value="">اختر المحافظة</option>
                  {visibleGovernorates.map((governorate) => <option key={governorate.governorateId} value={governorate.governorateId}>{governorate.name}</option>)}
                </Select>
              </Field>
              <Field label="اسم اللجنة">
                <Input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} placeholder="مثال: لجنة التنظيم" />
              </Field>
              <Button type="submit" className="w-full" disabled={!selectedGovernorateId}><span className="inline-flex items-center gap-2"><FiPlus /> إضافة لجنة</span></Button>
            </form>
          ) : (
            <EmptyState title="لا توجد صلاحية" description="إنشاء اللجان متاح لرئيس الكيان أو مساعد الرئيس أو منسق المحافظة." />
          )}
        </Card>

        <Card title="قائمة اللجان" subtitle="Committee roster">
          {committees.length === 0 ? (
            <EmptyState title="لا توجد لجان" description="اختر محافظة لعرض اللجان المسجلة داخلها." />
          ) : (
            <div className="space-y-3">
              {committees.map((committee) => (
                <div key={committee.committeeId} className="rounded-3xl border border-white/10 bg-white/5 p-4">
                  <p className="font-bold text-white">{committee.name}</p>
                  <p className="text-sm text-slate-400">{committee.governorateName}</p>
                  <p className="mt-2 text-xs text-slate-400">{formatDate(committee.createdAtUtc)}</p>
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}

function ReportsPage() {
  const { dashboard, members, tasks, complaints, activityLogs } = useApp();

  const downloadSheet = (filename: string, sheetName: string, rows: string[][]) => {
    const workbook = XLSX.utils.book_new();
    const worksheet = XLSX.utils.aoa_to_sheet(rows);
    XLSX.utils.book_append_sheet(workbook, worksheet, sheetName);
    XLSX.writeFile(workbook, filename, { bookType: 'xlsx' });
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
          <Button onClick={() => downloadSheet('الأعضاء.xlsx', 'الأعضاء', [['الاسم', 'البريد', 'الرقم القومي', 'تاريخ الميلاد', 'الدور', 'النقاط'], ...members.map((member) => [member.fullName, member.email, member.nationalId ?? '', member.birthDate ? formatDateOnly(member.birthDate) : '', roleLabel(member.role), String(member.points)])])}>
            <span className="inline-flex items-center gap-2"><FiDownload /> تصدير الأعضاء</span>
          </Button>
          <Button variant="secondary" onClick={() => downloadSheet('المهام.xlsx', 'المهام', [['العنوان', 'الوصف', 'الحالة'], ...tasks.map((task) => [task.title, task.description ?? '', task.isCompleted ? 'مكتملة' : 'قيد التنفيذ'])])}>
            <span className="inline-flex items-center gap-2"><FiDownload /> تصدير المهام</span>
          </Button>
          <Button variant="secondary" onClick={() => downloadSheet('الشكاوى.xlsx', 'الشكاوى', [['الموضوع', 'مقدم الشكوى', 'الرد الإداري', 'الحالة'], ...complaints.map((item) => [item.subject, item.memberName, item.adminReply ?? '', statusLabel(item.status)])])}>
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
          <div className="overflow-hidden rounded-3xl border border-white/10 bg-slate-950/60">
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
  const { user, dashboard, theme, toggleTheme, activityLogs } = useApp();

  return (
    <div className="space-y-6">
      <SectionTitle eyebrow={pageTitles.profile.eyebrow} title={pageTitles.profile.title} description={pageTitles.profile.description} />

      {user?.mustChangePassword && <PasswordChangeView />}

      <div className="grid gap-6 xl:grid-cols-[0.8fr_1.2fr]">
        <Card title="بيانات الحساب" subtitle="Profile">
          <div className="space-y-4">
            <div className="rounded-3xl border border-white/10 bg-white/5 p-5">
              <p className="text-xs text-slate-400">الاسم</p>
              <p className="mt-1 text-xl font-bold text-white">{user?.fullName}</p>
              <p className="mt-1 text-sm text-slate-400">{user?.email}</p>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <StatCard label="الدور" value={user?.role ?? '—'} accent="brand" />
              <StatCard label="النقاط" value={user?.points ?? 0} accent="success" />
            </div>
            <Button className="w-full" variant="secondary" onClick={toggleTheme}>تبديل الوضع إلى {theme === 'dark' ? 'الفاتح' : 'الداكن'}</Button>
          </div>
        </Card>

        <div className="space-y-6">
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
  const { isAuthenticated, section, clearError } = useApp();

  useEffect(() => {
    clearError();
  }, [clearError]);

  if (!isAuthenticated) {
    return <LoginView />;
  }

  const content: Record<SectionKey, ReactElement> = {
    overview: <OverviewPage />,
    news: <NewsPage />,
    members: <MembersPage />,
    tasks: <TasksPage />,
    complaints: <ComplaintsPage />,
    auditlogs: <AuditLogsPage />,
    committees: <CommitteesPage />,
    reports: <ReportsPage />,
    profile: <ProfilePage />
  };

  return (
    <AppShell>
      {content[section]}
    </AppShell>
  );
}

