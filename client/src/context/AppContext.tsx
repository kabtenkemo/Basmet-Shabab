import { createContext, useCallback, useContext, useEffect, useMemo, useState, type PropsWithChildren } from 'react';
import {
  adjustPoints,
  changePassword as submitPasswordChangeRequest,
  createComplaint,
  createMember,
  createNews,
  createTask,
  deleteTask,
  getComplaints,
  getDashboard,
  getLeaderboard,
  getMembers,
  getMe,
  getMyComplaints,
  getNews,
  getStoredToken,
  getTasks,
  grantPermission,
  grantRole,
  login,
  reviewComplaint,
  setStoredToken,
  updateTask
} from '../api';
import type {
  ActivityLogEntry,
  ComplaintFormState,
  ComplaintItem,
  ComplaintReviewState,
  DashboardMe,
  DashboardOverview,
  MemberAdminItem,
  MemberCreateFormState,
  MemberInfo,
  NewsCreateState,
  NewsItem,
  NavigationItem,
  PointFormState,
  SectionKey,
  TaskFormState,
  TaskItem,
  ThemeMode
} from '../types';

const defaultSearch = '';

const roleOrder: Record<string, number> = {
  President: 0,
  VicePresident: 1,
  CentralMember: 2,
  GovernorCoordinator: 3,
  GovernorCommitteeCoordinator: 4,
  CommitteeMember: 5
};

interface AppContextValue {
  token: string | null;
  user: MemberInfo | null;
  dashboard: DashboardOverview | null;
  leaderboard: DashboardOverview['topMembers'];
  members: MemberAdminItem[];
  tasks: TaskItem[];
  complaints: ComplaintItem[];
  news: NewsItem[];
  myComplaints: ComplaintItem[];
  activityLogs: ActivityLogEntry[];
  section: SectionKey;
  theme: ThemeMode;
  search: string;
  loading: boolean;
  error: string;
  isAuthenticated: boolean;
  canManageUsers: boolean;
  canCreateMembers: boolean;
  canManageComplaints: boolean;
  canManagePoints: boolean;
  canViewReports: boolean;
  canManageNews: boolean;
  navigation: NavigationItem[];
  setSection: (section: SectionKey) => void;
  setSearch: (value: string) => void;
  loginUser: (email: string, password: string) => Promise<void>;
  logout: () => void;
  refresh: () => Promise<void>;
  createMember: (form: MemberCreateFormState) => Promise<void>;
  changeRole: (memberId: string, role: string) => Promise<void>;
  assignPermission: (memberId: string, permissionKey: string) => Promise<void>;
  changePoints: (memberId: string, form: PointFormState) => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
  createTaskItem: (form: TaskFormState) => Promise<void>;
  updateTaskItem: (id: string, form: TaskFormState) => Promise<void>;
  deleteTaskItem: (id: string) => Promise<void>;
  createComplaintItem: (form: ComplaintFormState) => Promise<void>;
  createNewsItem: (form: NewsCreateState) => Promise<void>;
  reviewComplaintItem: (id: string, review: ComplaintReviewState) => Promise<void>;
  clearError: () => void;
  addActivity: (title: string, description: string, tone?: ActivityLogEntry['tone']) => void;
}

const AppContext = createContext<AppContextValue | undefined>(undefined);

const sectionLabels: Record<SectionKey, string> = {
  overview: 'الملخص',
  leaderboard: 'المتصدرين',
  news: 'الأخبار',
  members: 'الأعضاء',
  tasks: 'المهام',
  complaints: 'الشكاوى',
  auditlogs: 'سجل التدقيق',
  committees: 'اللجان',
  reports: 'التقارير',
  suggestions: 'المقترحات',
  profile: 'الملف الشخصي'
};

const navigationSeed: NavigationItem[] = [
  { key: 'overview', label: 'الملخص', icon: 'grid', roles: ['President', 'VicePresident', 'CentralMember', 'GovernorCoordinator', 'GovernorCommitteeCoordinator', 'CommitteeMember'] },
  { key: 'leaderboard', label: 'المتصدرين', icon: 'leaderboard', roles: ['President', 'VicePresident', 'CentralMember', 'GovernorCoordinator', 'GovernorCommitteeCoordinator', 'CommitteeMember'] },
  { key: 'news', label: 'الأخبار', icon: 'news', roles: ['President', 'VicePresident', 'CentralMember', 'GovernorCoordinator', 'GovernorCommitteeCoordinator', 'CommitteeMember'] },
  { key: 'members', label: 'الأعضاء', icon: 'users', roles: ['President', 'VicePresident', 'CentralMember', 'GovernorCoordinator', 'GovernorCommitteeCoordinator'] },
  { key: 'tasks', label: 'المهام', icon: 'check', roles: ['President', 'VicePresident', 'CentralMember', 'GovernorCoordinator', 'GovernorCommitteeCoordinator', 'CommitteeMember'] },
  { key: 'complaints', label: 'الشكاوى', icon: 'message', roles: ['President', 'VicePresident', 'CentralMember', 'GovernorCoordinator', 'GovernorCommitteeCoordinator', 'CommitteeMember'] },
  { key: 'auditlogs', label: 'سجل التدقيق', icon: 'activity', roles: ['President'] },
  { key: 'committees', label: 'اللجان', icon: 'layers', roles: ['President', 'VicePresident', 'GovernorCoordinator'] },
  { key: 'suggestions', label: 'المقترحات', icon: 'suggestions', roles: ['President', 'VicePresident', 'CentralMember', 'GovernorCoordinator', 'GovernorCommitteeCoordinator', 'CommitteeMember'] },
  { key: 'reports', label: 'التقارير', icon: 'chart', roles: ['President', 'VicePresident', 'CentralMember', 'GovernorCoordinator'] },
  { key: 'profile', label: 'الملف الشخصي', icon: 'profile', roles: ['President', 'VicePresident', 'CentralMember', 'GovernorCoordinator', 'GovernorCommitteeCoordinator', 'CommitteeMember'] }
];

function createLog(title: string, description: string, tone: ActivityLogEntry['tone'] = 'info'): ActivityLogEntry {
  return {
    id: crypto.randomUUID(),
    title,
    description,
    tone,
    createdAtUtc: new Date().toISOString()
  };
}

function hasPermission(member: MemberInfo | null, permissionKey: string) {
  return member?.permissions.some((permission) => permission.toLowerCase() === permissionKey.toLowerCase()) ?? false;
}

function sectionAllowed(member: MemberInfo | null, navigationItem: NavigationItem) {
  if (!member) {
    return false;
  }

  return navigationItem.roles.includes(member.role);
}

function mapMemberProfile(profile: DashboardMe): MemberInfo {
  return {
    id: profile.currentMemberId,
    fullName: profile.currentMemberName,
    email: profile.email,
    role: profile.role,
    nationalId: profile.nationalId,
    birthDate: profile.birthDate,
    governorName: profile.governorName,
    committeeName: profile.committeeName,
    points: profile.points,
    permissions: profile.permissions,
    mustChangePassword: profile.mustChangePassword
  };
}

export function AppProvider({ children }: PropsWithChildren) {
  const [token, setToken] = useState<string | null>(getStoredToken());
  const [user, setUser] = useState<MemberInfo | null>(null);
  const [dashboard, setDashboard] = useState<DashboardOverview | null>(null);
  const [members, setMembers] = useState<MemberAdminItem[]>([]);
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [complaints, setComplaints] = useState<ComplaintItem[]>([]);
  const [news, setNews] = useState<NewsItem[]>([]);
  const [myComplaints, setMyComplaints] = useState<ComplaintItem[]>([]);
  const [activityLogs, setActivityLogs] = useState<ActivityLogEntry[]>([]);
  const [section, setSection] = useState<SectionKey>('overview');
  const theme: ThemeMode = 'dark';
  const [search, setSearch] = useState(defaultSearch);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const canManageUsers = useMemo(() => {
    return user ? user.role === 'President' || user.role === 'VicePresident' || hasPermission(user, 'Users.Manage') : false;
  }, [user]);

  const canCreateMembers = useMemo(() => {
    if (!user) {
      return false;
    }

    return user.role === 'President'
      || user.role === 'VicePresident'
      || user.role === 'GovernorCoordinator'
      || user.role === 'GovernorCommitteeCoordinator'
      || hasPermission(user, 'Members.Create.GovernorCommitteeCoordinator')
      || hasPermission(user, 'Members.Create.CommitteeMember');
  }, [user]);

  const canManageComplaints = useMemo(() => {
    return user ? user.role === 'President' || user.role === 'VicePresident' || hasPermission(user, 'Complaints.Manage') : false;
  }, [user]);

  const canManagePoints = useMemo(() => {
    return user ? user.role === 'President' || user.role === 'VicePresident' || hasPermission(user, 'Points.Manage') : false;
  }, [user]);

  const canViewReports = useMemo(() => {
    return user ? user.role !== 'CommitteeMember' : false;
  }, [user]);

  const canManageNews = useMemo(() => {
    return user ? user.role === 'President' || user.role === 'VicePresident' : false;
  }, [user]);

  const navigation = useMemo(() => navigationSeed.filter((item) => sectionAllowed(user, item)), [user]);

  const appendActivity = useCallback((title: string, description: string, tone: ActivityLogEntry['tone'] = 'info') => {
    setActivityLogs((current) => [createLog(title, description, tone), ...current].slice(0, 30));
  }, []);

  const loadSession = useCallback(async (currentToken?: string | null) => {
    const authToken = currentToken ?? token;
    if (!authToken) {
      return;
    }

    setLoading(true);
    setError('');
    let isRetry = false;

    try {
      const profile = await getMe();
      const currentMember = mapMemberProfile(profile);

      setUser(currentMember);

      if (currentMember.mustChangePassword) {
        setDashboard(null);
        setMembers([]);
        setTasks([]);
        setComplaints([]);
        setNews([]);
        setMyComplaints([]);
        return;
      }

      // FIX: Use Promise.allSettled to handle partial failures gracefully
      // If one API call fails, others still complete instead of failing everything
      const [dashboardResult, tasksResult, complaintsResult, newsResult] = await Promise.allSettled([
        getDashboard().catch(err => {
          console.warn('Failed to load dashboard:', err);
          return null;
        }),
        getTasks().catch(err => {
          console.warn('Failed to load tasks:', err);
          return [];
        }),
        getMyComplaints().catch(err => {
          console.warn('Failed to load complaints:', err);
          return [];
        }),
        getNews().catch(err => {
          console.warn('Failed to load news:', err);
          return [];
        })
      ]);

      if (dashboardResult.status === 'fulfilled' && dashboardResult.value) {
        setDashboard(dashboardResult.value);
      }

      if (tasksResult.status === 'fulfilled') {
        setTasks(tasksResult.value || []);
      }

      if (complaintsResult.status === 'fulfilled') {
        setMyComplaints(complaintsResult.value || []);
      }

      if (newsResult.status === 'fulfilled') {
        setNews(newsResult.value || []);
      }

      if (currentMember.role === 'President' || currentMember.role === 'VicePresident' || hasPermission(currentMember, 'Users.Manage')) {
        const membersResult = await getMembers().catch(err => {
          console.warn('Failed to load members:', err);
          return [] as MemberAdminItem[];
        });
        setMembers(membersResult || []);
      } else {
        setMembers([]);
      }

      if (currentMember.role === 'President' || currentMember.role === 'VicePresident' || hasPermission(currentMember, 'Complaints.Manage')) {
        const complaintsListResult = await getComplaints().catch(err => {
          console.warn('Failed to load complaints list:', err);
          return [] as ComplaintItem[];
        });
        setComplaints(complaintsListResult || []);
      } else {
        setComplaints([]);
      }

      if (!navigationSeed.some((item) => item.key === section && sectionAllowed(currentMember, item))) {
        setSection('overview');
      }
    } catch (loadError) {
      const errorMsg = loadError instanceof Error ? loadError.message : 'تعذر تحميل البيانات';
      setError(errorMsg);
      console.error('Session load failed:', errorMsg);
    } finally {
      setLoading(false);
    }
  }, [section, token]);

  useEffect(() => {
    document.documentElement.classList.add('dark');
    document.documentElement.dir = 'rtl';
    document.documentElement.lang = 'ar';
  }, [theme]);

  useEffect(() => {
    setStoredToken(token);
    if (token) {
      void loadSession(token);
    } else {
      setUser(null);
      setDashboard(null);
      setMembers([]);
      setTasks([]);
      setComplaints([]);
      setNews([]);
      setMyComplaints([]);
      setSection('overview');
    }
  }, [loadSession, token]);

  useEffect(() => {
    if (!token) {
      return;
    }

    const interval = window.setInterval(() => {
      void loadSession(token);
    }, 45000);

    return () => window.clearInterval(interval);
  }, [loadSession, token]);

  const loginUser = useCallback(async (email: string, password: string) => {
    setLoading(true);
    setError('');
    try {
      // Trim and validate inputs
      const trimmedEmail = email.trim().toLowerCase();
      const trimmedPassword = password.trim();

      if (!trimmedEmail || !trimmedPassword) {
        throw new Error('البريد الإلكتروني وكلمة المرور مطلوبان.');
      }

      const response = await login(trimmedEmail, trimmedPassword);
      setUser({
        id: response.memberId,
        fullName: response.fullName,
        email: response.email.toLowerCase(),
        role: response.role,
        nationalId: response.nationalId,
        birthDate: response.birthDate,
        governorName: response.governorName,
        committeeName: response.committeeName,
        points: response.points,
        permissions: response.permissions,
        mustChangePassword: response.mustChangePassword
      });
      setToken(response.token);
      setStoredToken(response.token);
      setActivityLogs((current) => [createLog('تسجيل الدخول', `تم تسجيل دخول ${response.fullName}`, 'success'), ...current]);
      void loadSession(response.token);
    } catch (loginError) {
      const errorMessage = loginError instanceof Error ? loginError.message : 'تعذر تنفيذ عملية تسجيل الدخول';
      setError(errorMessage);
      console.error('Login failed:', errorMessage);
      throw loginError;
    } finally {
      setLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    setToken(null);
    setStoredToken(null);
    setUser(null);
    setDashboard(null);
    setMembers([]);
    setTasks([]);
    setComplaints([]);
    setNews([]);
    setMyComplaints([]);
    setSection('overview');
    setSearch('');
    setError('');
    appendActivity('تسجيل الخروج', 'تمت مغادرة الجلسة الحالية', 'warning');
  }, [appendActivity]);

  const createMemberItem = useCallback(async (form: MemberCreateFormState) => {
    setLoading(true);
    setError('');
    try {
      await createMember(form);
      appendActivity('إنشاء عضو', `تم إنشاء حساب ${form.fullName}`, 'success');
      await loadSession();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر إنشاء العضو');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession]);

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    setLoading(true);
    setError('');
    try {
      await submitPasswordChangeRequest(currentPassword, newPassword);
      appendActivity('تغيير كلمة المرور', 'تم تغيير كلمة المرور الإلزامية بنجاح', 'success');
      await loadSession(token);
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر تغيير كلمة المرور');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession, token]);

  const changeRole = useCallback(async (memberId: string, role: string) => {
    setLoading(true);
    setError('');
    try {
      await grantRole(memberId, role);
      appendActivity('تحديث الدور', `تم تحديث الدور إلى ${role}`, 'success');
      await loadSession();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر تحديث الدور');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession]);

  const assignPermission = useCallback(async (memberId: string, permissionKey: string) => {
    setLoading(true);
    setError('');
    try {
      await grantPermission(memberId, permissionKey);
      appendActivity('منح صلاحية', `تمت إضافة ${permissionKey}`, 'success');
      await loadSession();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر إضافة الصلاحية');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession]);

  const changePoints = useCallback(async (memberId: string, form: PointFormState) => {
    setLoading(true);
    setError('');
    try {
      await adjustPoints(memberId, form);
      appendActivity('تعديل النقاط', `تم تعديل النقاط بمقدار ${form.amount}`, 'success');
      await loadSession();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر تعديل النقاط');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession]);

  const createTaskItem = useCallback(async (form: TaskFormState) => {
    setLoading(true);
    setError('');
    try {
      await createTask(form);
      appendActivity('إضافة مهمة', `تمت إضافة ${form.title}`, 'success');
      await loadSession();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر إنشاء المهمة');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession]);

  const updateTaskItem = useCallback(async (id: string, form: TaskFormState) => {
    setLoading(true);
    setError('');
    try {
      await updateTask(id, form);
      appendActivity('تحديث مهمة', `تم تحديث ${form.title}`, 'success');
      await loadSession();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر تحديث المهمة');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession]);

  const deleteTaskItem = useCallback(async (id: string) => {
    setLoading(true);
    setError('');
    try {
      await deleteTask(id);
      appendActivity('حذف مهمة', 'تم حذف مهمة من القائمة', 'warning');
      await loadSession();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر حذف المهمة');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession]);

  const createComplaintItem = useCallback(async (form: ComplaintFormState) => {
    setLoading(true);
    setError('');
    try {
      await createComplaint(form);
      appendActivity('إرسال شكوى', `تم إرسال ${form.subject}`, 'success');
      await loadSession();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر إرسال الشكوى');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession]);

  const createNewsItem = useCallback(async (form: NewsCreateState) => {
    setLoading(true);
    setError('');
    try {
      await createNews(form);
      appendActivity('نشر خبر', `تم نشر خبر جديد: ${form.title}`, 'success');
      await loadSession();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر نشر الخبر');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession]);

  const reviewComplaintItem = useCallback(async (id: string, review: ComplaintReviewState) => {
    setLoading(true);
    setError('');
    try {
      await reviewComplaint(id, review);
      appendActivity('مراجعة شكوى', 'تم حفظ مراجعة الشكوى', 'success');
      await loadSession();
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'تعذر حفظ مراجعة الشكوى');
      throw actionError;
    } finally {
      setLoading(false);
    }
  }, [appendActivity, loadSession]);

  const value = useMemo<AppContextValue>(() => ({
    token,
    user,
    dashboard,
    leaderboard: dashboard?.topMembers ?? [],
    members,
    tasks,
    complaints,
    news,
    myComplaints,
    activityLogs,
    section,
    theme,
    search,
    loading,
    error,
    isAuthenticated: Boolean(token && user),
    canManageUsers,
    canCreateMembers,
    canManageComplaints,
    canManagePoints,
    canViewReports,
    canManageNews,
    navigation,
    setSection,
    setSearch,
    loginUser,
    logout,
    refresh: () => loadSession(),
    createMember: createMemberItem,
    changeRole,
    assignPermission,
    changePoints,
    changePassword,
    createTaskItem,
    updateTaskItem,
    deleteTaskItem,
    createComplaintItem,
    createNewsItem,
    reviewComplaintItem,
    clearError: () => setError(''),
    addActivity: appendActivity
  }), [
    activityLogs,
    appendActivity,
    assignPermission,
    canManageComplaints,
    canManagePoints,
    canManageUsers,
    canViewReports,
    canManageNews,
    complaints,
    news,
    createComplaintItem,
    createNewsItem,
    createMemberItem,
    createTaskItem,
    dashboard,
    deleteTaskItem,
    error,
    loadSession,
    loginUser,
    logout,
    members,
    myComplaints,
    navigation,
    reviewComplaintItem,
    search,
    section,
    tasks,
    theme,
    token,
    updateTaskItem,
    user,
    changePoints,
    changeRole,
    changePassword,
  ]);

  return <AppContext.Provider value={value}>{children}</AppContext.Provider>;
}

export function useApp() {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error('useApp must be used within AppProvider');
  }

  return context;
}
