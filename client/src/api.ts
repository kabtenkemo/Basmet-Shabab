import axios, { AxiosHeaders, type AxiosRequestConfig, type AxiosResponse } from 'axios';
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
  ImportantContactCreateState,
  ImportantContactItem,
  LeaderboardEntry,
  MemberAdminItem,
  MemberCreateFormState,
  PointFormState,
  TeamJoinRequest,
  TeamJoinRequestCreateState,
  TeamJoinRequestReviewState,
  TaskFormState,
  TaskItem,
  SuggestionItem,
  SuggestionFormState
} from './types';

const productionApiBaseUrl = 'https://basmet-shabab.runasp.net';
const authTokenKey = 'team-management-token';
const unauthorizedEventName = 'basma:unauthorized';
const isLocalRuntime = typeof window !== 'undefined' && (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1');
const netlifyHostnameSuffix = '.netlify.app';
const vercelHostnameSuffix = '.vercel.app';

function resolveBaseUrl() {
  const env = import.meta.env as Record<string, string | undefined>;
  const configured = env.VITE_API_BASE_URL?.trim();
  const normalizedConfigured = configured ? configured.replace(/\/+$/, '') : '';

  if (typeof window !== 'undefined') {
    const hostname = window.location.hostname;
    const isLocalhost = hostname === 'localhost' || hostname === '127.0.0.1';
    const isNetlify = hostname.endsWith(netlifyHostnameSuffix);
    const isVercel = hostname.endsWith(vercelHostnameSuffix);

    if (isLocalhost) {
      return '';
    }

    // On Netlify, always use same-origin /api rewrite to avoid browser CORS/preflight
    // issues and direct TLS reachability differences across user networks.
    if (isNetlify || isVercel) {
      return '';
    }
  }

  if (normalizedConfigured) {
    return normalizedConfigured;
  }

  return productionApiBaseUrl;
}

function attachInterceptors(instance: ReturnType<typeof axios.create>) {
  instance.interceptors.request.use((config) => {
    const token = localStorage.getItem(authTokenKey);
    const headers = AxiosHeaders.from(config.headers ?? {});
    const requestUrl = String(config.url ?? '').toLowerCase();
    const isLoginRequest = requestUrl.includes('/api/auth/login');

    headers.set('Content-Type', 'application/json');
    if (token && !isLoginRequest) {
      headers.set('Authorization', `Bearer ${token}`);
    } else {
      headers.delete('Authorization');
    }

    config.headers = headers;
    return config;
  });

  instance.interceptors.response.use(
    (response) => response,
    (error) => {
      if (error.response?.status === 401) {
        const requestUrl = String(error.config?.url ?? '').toLowerCase();
        const isLoginRequest = requestUrl.includes('/api/auth/login');

        localStorage.removeItem(authTokenKey);

        if (!isLoginRequest && typeof window !== 'undefined') {
          window.dispatchEvent(new CustomEvent(unauthorizedEventName));
        }
      }

      return Promise.reject(error);
    }
  );
}

const apiBaseUrl = resolveBaseUrl();

const api = axios.create({
  baseURL: apiBaseUrl,
  timeout: 20000
});

const directApi = axios.create({
  baseURL: productionApiBaseUrl,
  timeout: 20000
});

attachInterceptors(api);
attachInterceptors(directApi);

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
      return 'البريد الإلكتروني أو كلمة المرور غير صحيحة.';
    }

    if (error.response?.status === 403) {
      return 'ليس لديك صلاحية لتنفيذ هذا الطلب.';
    }

    if (error.response?.status === 404) {
      return 'الخدمة المطلوبة غير متاحة حاليًا. تحقق من إعدادات الربط بين الموقع والخادم.';
    }

    if (error.response?.status === 500) {
      const requestUrl = String(error.config?.url ?? '').toLowerCase();
      if (requestUrl.includes('/api/auth/login')) {
        return 'تعذر تسجيل الدخول الآن بسبب خطأ في الخادم. الخادم قد يكون في حالة إعادة تشغيل — حاول مرة أخرى بعد دقيقة.';
      }
      return 'حدث خطأ في الخادم. يرجى المحاولة مرة أخرى لاحقًا.';
    }

    if (error.code === 'ECONNABORTED') {
      return 'انتهت مهلة الاتصال بالخادم. حاول مرة أخرى بعد لحظات.';
    }

    if (
      error.code === 'ERR_NETWORK'
      || error.code === 'ENOTFOUND'
      || error.code === 'NETWORK_ERROR'
      || error.code === 'ECONNREFUSED'
      || error.code === 'ECONNRESET'
      || error.message?.toLowerCase().includes('network')
    ) {
      return 'تعذر الوصول إلى الخادم. تحقق من اتصال الإنترنت أو إعدادات نشر الـ API.';
    }
  }

  return 'تعذر تنفيذ الطلب. حاول مرة أخرى.';
}

function shouldRetryWithDirectApi(error: unknown) {
  if (!axios.isAxiosError(error)) {
    return false;
  }

  // Avoid expensive fallback retries while developing locally.
  if (isLocalRuntime) {
    return false;
  }

  if (apiBaseUrl === productionApiBaseUrl) {
    return false;
  }

  const responseHeaders = error.response?.headers as Record<string, string | undefined> | undefined;
  const cacheStatus = String(responseHeaders?.['cache-status'] ?? '').toLowerCase();
  const server = String(responseHeaders?.server ?? '').toLowerCase();
  const hasNetlifyRequestId = Boolean(responseHeaders?.['x-nf-request-id']);
  const isNetlifyEdgeResponse = hasNetlifyRequestId
    || cacheStatus.includes('netlify edge')
    || server.includes('netlify');
  const isNetlifyRuntime = typeof window !== 'undefined' && window.location.hostname.endsWith(netlifyHostnameSuffix);
  const requestUrl = String(error.config?.url ?? '').toLowerCase();
  const isApiRequest = requestUrl.startsWith('/api/');
  const status = error.response?.status;
  const isServerError = typeof status === 'number' && status >= 500 && status <= 504;
  
  // On Netlify, the proxy layer can mask transient upstream issues as 5xx even when
  // the direct origin is reachable. Allow a one-shot cross-origin fallback for 5xx
  // responses — the backend CORS policy permits *.netlify.app, so this is safe.
  if (isNetlifyRuntime) {
    return isApiRequest && isServerError;
}

  // Netlify edge can occasionally return an empty 500 for proxied API calls.
  // Retry once against the direct API origin before surfacing an error.
  if (status === 500 && (isNetlifyEdgeResponse || (isNetlifyRuntime && isApiRequest))) {
    return true;
  }

  if (status === 404) {
    return true;
  }

  if (status === 502 || status === 503 || status === 504) {
    return true;
  }

  return !error.response && (
    error.code === 'ERR_NETWORK'
    || error.code === 'ECONNREFUSED'
    || error.code === 'ENOTFOUND'
    || error.code === 'ECONNRESET'
    || error.message?.toLowerCase().includes('network')
  );
}

async function unwrap<T>(operation: Promise<AxiosResponse<T>>, fallbackOperation?: (() => Promise<AxiosResponse<T>>) | null): Promise<T> {
  try {
    const response = await operation;
    return response.data;
  } catch (error) {
    if (fallbackOperation && shouldRetryWithDirectApi(error)) {
      try {
        const fallbackResponse = await fallbackOperation();
        return fallbackResponse.data;
      } catch (fallbackError) {
        throw new Error(getErrorMessage(fallbackError));
      }
    }

    throw new Error(getErrorMessage(error));
  }
}

function request<T>(config: AxiosRequestConfig, allowDirectFallback = true) {
  const fallbackOperation = allowDirectFallback
    ? () => directApi.request<T>({ ...config, baseURL: productionApiBaseUrl })
    : null;

  return unwrap(api.request<T>(config), fallbackOperation);
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

export function getUnauthorizedEventName() {
  return unauthorizedEventName;
}

export async function login(email: string, password: string) {
  return request<AuthResponse>({
    method: 'POST',
    url: '/api/auth/login',
    data: { email: email.trim().toLowerCase(), password }
  });
}

export async function changePassword(currentPassword: string, newPassword: string) {
  return request({ method: 'POST', url: '/api/auth/change-password', data: { currentPassword, newPassword } });
}

export async function getMe() {
  return request<DashboardMe>({ method: 'GET', url: '/api/members/me' });
}

export async function getDashboard() {
  return request<DashboardOverview>({ method: 'GET', url: '/api/dashboard/overview' });
}

export async function getLeaderboard() {
  return request<LeaderboardEntry[]>({ method: 'GET', url: '/api/dashboard/leaderboard' });
}

export async function getMembers() {
  return request<MemberAdminItem[]>({ method: 'GET', url: '/api/members' });
}

export async function getImportantContacts() {
  return request<ImportantContactItem[]>({ method: 'GET', url: '/api/important-contacts' });
}

export async function createImportantContact(form: ImportantContactCreateState) {
  return request<ImportantContactItem>({
    method: 'POST',
    url: '/api/important-contacts',
    data: {
      fullName: form.fullName.trim(),
      phoneNumber: form.phoneNumber.trim(),
      positionTitle: form.positionTitle.trim(),
      domain: form.domain.trim()
    }
  });
}

export async function deleteImportantContact(contactId: string) {
  return request({ method: 'DELETE', url: `/api/important-contacts/${contactId}` });
}

export async function deleteMember(memberId: string) {
  return request({ method: 'DELETE', url: `/api/members/${memberId}` });
}

export async function createMember(form: MemberCreateFormState) {
  return request<MemberAdminItem>({
    method: 'POST',
    url: '/api/members',
    data: {
      ...form,
      email: form.email.trim().toLowerCase(),
      governorateId: form.governorateId || null,
      committeeId: form.committeeId || null,
      birthDate: form.birthDate || null
    }
  });
}

export async function getGovernorates() {
  return request<GovernorateOption[]>({ method: 'GET', url: '/api/governorates' });
}

export async function updateGovernorateJoinVisibility(governorateId: string, isVisibleInJoinForm: boolean) {
  return request<GovernorateOption>({
    method: 'PATCH',
    url: `/api/governorates/${governorateId}/join-visibility`,
    data: { isVisibleInJoinForm }
  });
}

export async function updateCommitteeJoinVisibility(governorateId: string, committeeId: string, isVisibleInJoinForm: boolean) {
  return request<CommitteeOption>({
    method: 'PATCH',
    url: `/api/governorates/${governorateId}/committees/${committeeId}/join-visibility`,
    data: { isVisibleInJoinForm }
  });
}

export async function getGovernorateCommittees(governorateId: string, kind: 'default' | 'club' | 'all' = 'default') {
  const params = new URLSearchParams();

  if (kind === 'club') {
    params.set('kind', 'club');
  } else if (kind === 'all') {
    params.set('kind', 'all');
  }

  const query = params.toString();
  const url = `/api/governorates/${governorateId}/committees${query ? `?${query}` : ''}`;

  return request<CommitteeOption[]>({ method: 'GET', url });
}

export async function createCommittee(governorateId: string, form: CommitteeCreateFormState, isStudentClub = false) {
  return request<CommitteeOption>({
    method: 'POST',
    url: `/api/governorates/${governorateId}/committees`,
    data: { ...form, isStudentClub }
  });
}

export async function deleteCommittee(governorateId: string, committeeId: string) {
  return request({ method: 'DELETE', url: `/api/governorates/${governorateId}/committees/${committeeId}` });
}

export async function createJoinRequest(form: TeamJoinRequestCreateState) {
  return request<TeamJoinRequest>({
    method: 'POST',
    url: '/api/join-requests',
    data: {
      applicationType: form.applicationType,
      fullName: form.fullName.trim(),
      email: form.email.trim().toLowerCase(),
      phoneNumber: form.phoneNumber.trim(),
      nationalId: form.nationalId.trim(),
      birthDate: form.birthDate || null,
      governorateId: form.governorateId,
      committeeId: form.committeeId || null,
      motivation: form.motivation.trim(),
      experience: form.experience.trim() || null
    }
  });
}

export async function getJoinRequests() {
  return request<TeamJoinRequest[]>({ method: 'GET', url: '/api/join-requests' });
}

export async function reviewJoinRequest(id: string, form: TeamJoinRequestReviewState) {
  return request({ method: 'PUT', url: `/api/join-requests/${id}/review`, data: form });
}

export async function grantRole(memberId: string, role: string) {
  return request({ method: 'POST', url: `/api/members/${memberId}/role`, data: { role } });
}

export async function grantPermission(memberId: string, permissionKey: string) {
  return request({ method: 'POST', url: `/api/members/${memberId}/permissions`, data: { permissionKey } });
}

export async function adjustPoints(memberId: string, form: PointFormState) {
  return request({ method: 'POST', url: `/api/members/${memberId}/points`, data: { amount: Number(form.amount), reason: form.reason } });
}

export async function resetMemberPassword(memberId: string) {
  return request({ method: 'POST', url: `/api/members/${memberId}/reset-password`, data: {} });
}

export async function getTasks() {
  return request<TaskItem[]>({ method: 'GET', url: '/api/tasks' });
}

export async function createTask(task: TaskFormState) {
  return request<TaskItem>({
    method: 'POST',
    url: '/api/tasks',
    data: {
      title: task.title,
      description: task.description || null,
      dueDate: task.dueDate || null,
      audienceType: task.audienceType,
      targetRoles: task.audienceType === 'Roles' ? task.targetRoles : [],
      targetMemberIds: task.audienceType === 'Members' ? task.targetMemberIds : [],
      isCompleted: task.isCompleted
    }
  });
}

export async function updateTask(id: string, task: TaskFormState) {
  return request<TaskItem>({
    method: 'PUT',
    url: `/api/tasks/${id}`,
    data: {
      title: task.title,
      description: task.description || null,
      dueDate: task.dueDate || null,
      audienceType: task.audienceType,
      targetRoles: task.audienceType === 'Roles' ? task.targetRoles : [],
      targetMemberIds: task.audienceType === 'Members' ? task.targetMemberIds : [],
      isCompleted: task.isCompleted
    }
  });
}

export async function deleteTask(id: string) {
  return request({ method: 'DELETE', url: `/api/tasks/${id}` });
}

export async function completeTask(id: string) {
  return request<TaskItem>({ method: 'POST', url: `/api/tasks/${id}/complete` });
}

export async function getMyComplaints() {
  return request<ComplaintItem[]>({ method: 'GET', url: '/api/complaints/mine' });
}

export async function getComplaints() {
  return request<ComplaintItem[]>({ method: 'GET', url: '/api/complaints' });
}

export async function getComplaint(id: string) {
  return request<ComplaintDetail>({ method: 'GET', url: `/api/complaints/${id}` });
}

export async function createComplaint(form: ComplaintFormState) {
  return request<ComplaintItem>({ method: 'POST', url: '/api/complaints', data: form });
}

export async function reviewComplaint(id: string, review: ComplaintReviewState) {
  return request({ method: 'PUT', url: `/api/complaints/${id}`, data: review });
}

export async function commentComplaint(id: string, form: ComplaintCommentState) {
  return request({ method: 'POST', url: `/api/complaints/${id}/comments`, data: form });
}

export async function escalateComplaint(id: string, form: ComplaintEscalateState) {
  return request({ method: 'POST', url: `/api/complaints/${id}/escalate`, data: form });
}

export async function getAuditLogs(filters: AuditLogFilters) {
  const params = new URLSearchParams();

  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== null && String(value).trim() !== '') {
      params.set(key, String(value));
    }
  });

  return request<AuditLogPage>({ method: 'GET', url: `/api/auditlogs?${params.toString()}` });
}

export async function getNews() {
  return request<NewsItem[]>({ method: 'GET', url: '/api/news' });
}

export async function createNews(form: NewsCreateState) {
  return request<NewsItem>({ method: 'POST', url: '/api/news', data: form });
}

export async function deleteNews(id: string) {
  return request({ method: 'DELETE', url: `/api/news/${id}` });
}

export async function getSuggestions(status?: string) {
  const params = new URLSearchParams();
  if (status) {
    params.set('status', status);
  }

  return request<SuggestionItem[]>({ method: 'GET', url: `/api/suggestions${params.toString() ? `?${params.toString()}` : ''}` });
}

export async function createSuggestion(form: SuggestionFormState) {
  return request<SuggestionItem>({ method: 'POST', url: '/api/suggestions', data: form });
}

export async function voteSuggestion(id: string, isAcceptance: boolean) {
  return request<SuggestionItem>({ method: 'POST', url: `/api/suggestions/${id}/vote`, data: { isAcceptance } });
}
