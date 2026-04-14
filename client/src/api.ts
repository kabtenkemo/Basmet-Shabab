import axios from 'axios';
import type {
  AuditLogFilters,
  AuditLogPage,
  AuthResponse,
  ComplaintCommentState,
  ComplaintFormState,
  ComplaintDetail,
  ComplaintItem,
  ComplaintEscalateState,
  ComplaintReviewState,
  DashboardMe,
  DashboardOverview,
  NewsCreateState,
  NewsItem,
  CommitteeCreateFormState,
  CommitteeOption,
  GovernorateOption,
  LeaderboardEntry,
  MemberAdminItem,
  MemberCreateFormState,
  MemberInfo,
  PointFormState,
  TaskFormState,
  TaskItem,
  SuggestionItem,
  SuggestionFormState
} from './types';

const baseURL = '';
const authTokenKey = 'team-management-token';

const api = axios.create({
  baseURL,
  headers: {
    'Content-Type': 'application/json'
  },
  timeout: 20000
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(authTokenKey);
  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

// Response interceptor to handle errors globally
// FIX: Automatically clear token on 401 to prevent stale sessions
api.interceptors.response.use(
  (response) => response,
  (error) => {
    // Handle 401 - clear token and require login
    if (error.response?.status === 401) {
      localStorage.removeItem(authTokenKey);
      window.location.href = '/';
    }
    return Promise.reject(error);
  }
);

function getErrorMessage(error: unknown) {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { message?: string; detail?: string; title?: string } | string | undefined;
    if (typeof data === 'string') {
      return data;
    }

    if (data?.message) {
      return data.message;
    }

    if (data?.detail) {
      return data.detail;
    }

    if (data?.title) {
      return data.title;
    }

    if (error.response?.status === 401) {
      return 'انتهت جلستك. يرجى تسجيل الدخول مرة أخرى.';
    }

    if (error.response?.status === 403) {
      return 'ليس لديك صلاحية لتنفيذ هذا الطلب.';
    }

    if (error.response?.status === 500) {
      return 'خطأ في الخادم. يرجى محاولة لاحقاً.';
    }

    if (error.code === 'ECONNABORTED') {
      return 'انقطع الاتصال. يرجى التحقق من اتصال الإنترنت.';
    }

    if (error.code === 'ENOTFOUND' || error.code === 'NETWORK_ERROR') {
      return 'لا يمكن الوصول للخادم. تحقق من الاتصال.';
    }
  }

  return 'تعذر تنفيذ الطلب';
}

async function unwrap<T>(operation: Promise<{ data: T }>): Promise<T> {
  try {
    const response = await operation;
    return response.data;
  } catch (error) {
    throw new Error(getErrorMessage(error));
  }
}

export function setStoredToken(token: string | null) {
  if (token) {
    localStorage.setItem(authTokenKey, token);
  } else {
    localStorage.removeItem(authTokenKey);
  }
}

export function getStoredToken() {
  return localStorage.getItem(authTokenKey);
}

export async function login(email: string, password: string) {
  // Never log passwords or sensitive data in production
  const response = await unwrap(api.post<AuthResponse>('/api/auth/login', { email: email.trim(), password: password.trim() }));
  return response;
}

export async function changePassword(currentPassword: string, newPassword: string) {
  return unwrap(api.post('/api/auth/change-password', { currentPassword, newPassword }));
}

export async function getMe() {
  return unwrap(api.get<DashboardMe>('/api/members/me'));
}

export async function getDashboard() {
  return unwrap(api.get<DashboardOverview>('/api/dashboard/overview'));
}

export async function getLeaderboard() {
  return unwrap(api.get<LeaderboardEntry[]>('/api/dashboard/leaderboard'));
}

export async function getMembers() {
  return unwrap(api.get<MemberAdminItem[]>('/api/members'));
}

export async function createMember(form: MemberCreateFormState) {
  return unwrap(api.post<MemberAdminItem>('/api/members', {
    ...form,
    governorateId: form.governorateId || null,
    committeeId: form.committeeId || null,
    birthDate: form.birthDate || null
  }));
}

export async function getGovernorates() {
  return unwrap(api.get<GovernorateOption[]>('/api/governorates'));
}

export async function getGovernorateCommittees(governorateId: string) {
  return unwrap(api.get<CommitteeOption[]>(`/api/governorates/${governorateId}/committees`));
}

export async function createCommittee(governorateId: string, form: CommitteeCreateFormState) {
  return unwrap(api.post<CommitteeOption>(`/api/governorates/${governorateId}/committees`, form));
}

export async function grantRole(memberId: string, role: string) {
  return unwrap(api.post(`/api/members/${memberId}/role`, { role }));
}

export async function grantPermission(memberId: string, permissionKey: string) {
  return unwrap(api.post(`/api/members/${memberId}/permissions`, { permissionKey }));
}

export async function adjustPoints(memberId: string, form: PointFormState) {
  return unwrap(api.post(`/api/members/${memberId}/points`, { amount: Number(form.amount), reason: form.reason }));
}

export async function getTasks() {
  return unwrap(api.get<TaskItem[]>('/api/tasks'));
}

export async function createTask(task: TaskFormState) {
  return unwrap(api.post<TaskItem>('/api/tasks', {
    title: task.title,
    description: task.description || null,
    dueDate: task.dueDate || null,
    audienceType: task.audienceType,
    targetRoles: task.audienceType === 'Roles' ? task.targetRoles : [],
    targetMemberIds: task.audienceType === 'Members' ? task.targetMemberIds : [],
    isCompleted: task.isCompleted
  }));
}

export async function updateTask(id: string, task: TaskFormState) {
  return unwrap(api.put<TaskItem>(`/api/tasks/${id}`, {
    title: task.title,
    description: task.description || null,
    dueDate: task.dueDate || null,
    audienceType: task.audienceType,
    targetRoles: task.audienceType === 'Roles' ? task.targetRoles : [],
    targetMemberIds: task.audienceType === 'Members' ? task.targetMemberIds : [],
    isCompleted: task.isCompleted
  }));
}

export async function deleteTask(id: string) {
  return unwrap(api.delete(`/api/tasks/${id}`));
}

export async function getMyComplaints() {
  return unwrap(api.get<ComplaintItem[]>('/api/complaints/mine'));
}

export async function getComplaints() {
  return unwrap(api.get<ComplaintItem[]>('/api/complaints'));
}

export async function getComplaint(id: string) {
  return unwrap(api.get<ComplaintDetail>(`/api/complaints/${id}`));
}

export async function createComplaint(form: ComplaintFormState) {
  return unwrap(api.post<ComplaintItem>('/api/complaints', form));
}

export async function reviewComplaint(id: string, review: ComplaintReviewState) {
  return unwrap(api.put(`/api/complaints/${id}`, review));
}

export async function commentComplaint(id: string, form: ComplaintCommentState) {
  return unwrap(api.post(`/api/complaints/${id}/comments`, form));
}

export async function escalateComplaint(id: string, form: ComplaintEscalateState) {
  return unwrap(api.post(`/api/complaints/${id}/escalate`, form));
}

export async function getAuditLogs(filters: AuditLogFilters) {
  const params = new URLSearchParams();

  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== null && String(value).trim() !== '') {
      params.set(key, String(value));
    }
  });

  return unwrap(api.get<AuditLogPage>(`/api/auditlogs?${params.toString()}`));
}

export async function getNews() {
  return unwrap(api.get<NewsItem[]>('/api/news'));
}

export async function createNews(form: NewsCreateState) {
  return unwrap(api.post<NewsItem>('/api/news', form));
}

export async function getSuggestions(status?: string) {
  const params = new URLSearchParams();
  if (status) {
    params.set('status', status);
  }
  return unwrap(api.get<SuggestionItem[]>(`/api/suggestions${params.toString() ? '?' + params.toString() : ''}`));
}

export async function createSuggestion(form: SuggestionFormState) {
  return unwrap(api.post<SuggestionItem>('/api/suggestions', form));
}

export async function voteSuggestion(id: string, isAcceptance: boolean) {
  return unwrap(api.post<SuggestionItem>(`/api/suggestions/${id}/vote`, { isAcceptance }));
}
