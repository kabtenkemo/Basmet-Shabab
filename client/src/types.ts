export type Role =
  | 'President'
  | 'VicePresident'
  | 'CentralMember'
  | 'GovernorCoordinator'
  | 'GovernorCommitteeCoordinator'
  | 'CommitteeMember';

export type SectionKey =
  | 'overview'
  | 'leaderboard'
  | 'news'
  | 'joinrequests'
  | 'members'
  | 'studentclubs'
  | 'tasks'
  | 'complaints'
  | 'auditlogs'
  | 'committees'
  | 'importantcontacts'
  | 'suggestions'
  | 'reports'
  | 'profile';

export type ThemeMode = 'dark';

export type AuthResponse = {
  memberId: string;
  fullName: string;
  email: string;
  role: Role;
  nationalId: string | null;
  birthDate: string | null;
  governorName: string | null;
  committeeName: string | null;
  points: number;
  permissions: string[];
  mustChangePassword: boolean;
  token: string;
  expiresAtUtc: string;
};

export type MemberInfo = {
  id: string;
  fullName: string;
  email: string;
  role: Role;
  nationalId: string | null;
  birthDate: string | null;
  governorName: string | null;
  committeeName: string | null;
  points: number;
  permissions: string[];
  mustChangePassword: boolean;
};

export type LeaderboardEntry = {
  memberId: string;
  fullName: string;
  role: Role;
  points: number;
  rank: number;
};

export type DashboardOverview = {
  currentMemberId: string;
  currentMemberName: string;
  role: Role;
  points: number;
  totalMembers: number;
  openComplaints: number;
  topMembers: LeaderboardEntry[];
};

export type DashboardMe = {
  currentMemberId: string;
  currentMemberName: string;
  email: string;
  role: Role;
  nationalId: string | null;
  birthDate: string | null;
  governorName: string | null;
  committeeName: string | null;
  points: number;
  permissions: string[];
  mustChangePassword: boolean;
};

export type MemberAdminItem = {
  memberId: string;
  fullName: string;
  email: string;
  role: Role;
  nationalId: string | null;
  birthDate: string | null;
  governorName: string | null;
  committeeName: string | null;
  points: number;
  permissions: string[];
  createdAtUtc: string;
};

export type TaskItem = {
  id: string;
  title: string;
  description: string | null;
  audienceType: TaskAudienceType | string;
  isCompleted: boolean;
  dueDate: string | null;
  createdAtUtc: string;
  targetRoles: Role[];
  targetMemberIds: string[];
};

export type TaskFormState = {
  title: string;
  description: string;
  dueDate: string;
  audienceType: TaskAudienceType;
  targetRoles: Role[];
  targetMemberIds: string[];
  isCompleted: boolean;
};

export type ComplaintItem = {
  id: string;
  memberId: string;
  memberName: string;
  subject: string;
  message: string;
  status: 'Open' | 'InReview' | 'Resolved' | 'Rejected' | string;
  adminReply: string | null;
  priority: 'Low' | 'Medium' | 'High' | string;
  escalationLevel: number;
  lastActionDateUtc: string;
  assignedToMemberId: string | null;
  assignedToMemberName: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  history?: ComplaintHistoryItem[];
};

export type ComplaintHistoryItem = {
  id: string;
  complaintId: string;
  action: 'Created' | 'Escalated' | 'Resolved' | 'Commented' | 'Assigned' | string;
  performedByUserId: string | null;
  performedByUserName: string | null;
  notes: string | null;
  timestampUtc: string;
};

export type ComplaintDetail = ComplaintItem & {
  history: ComplaintHistoryItem[];
};

export type TaskAudienceType = 'All' | 'Members' | 'Roles';

export type ComplaintFormState = {
  subject: string;
  message: string;
  priority: 'Low' | 'Medium' | 'High';
};

export type ComplaintReviewState = {
  status: string;
  adminReply: string;
};

export type ComplaintCommentState = {
  notes: string;
};

export type ComplaintEscalateState = {
  notes: string;
};

export type NewsAudienceType = 'All' | 'Roles' | 'Members';

export type NewsItem = {
  id: string;
  title: string;
  content: string;
  audienceType: NewsAudienceType | string;
  createdByMemberId: string;
  createdByName: string;
  createdAtUtc: string;
  targetRoles: Role[];
  targetMemberIds: string[];
};

export type NewsCreateState = {
  title: string;
  content: string;
  audienceType: NewsAudienceType;
  targetRoles: Role[];
  targetMemberIds: string[];
};

export type MemberCreateFormState = {
  fullName: string;
  email: string;
  nationalId: string;
  birthDate: string;
  role: Role;
  governorateId: string;
  committeeId: string;
};

export type PointFormState = {
  amount: string;
  reason: string;
};

export type ActivityLogEntry = {
  id: string;
  title: string;
  description: string;
  createdAtUtc: string;
  tone: 'info' | 'success' | 'warning';
};

export type AuditLogItem = {
  id: string;
  userId: string | null;
  userName: string;
  actionType: string;
  entityName: string;
  entityId: string | null;
  oldValuesJson: string | null;
  newValuesJson: string | null;
  timestampUtc: string;
  ipAddress: string | null;
};

export type AuditLogPage = {
  items: AuditLogItem[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type AuditLogFilters = {
  userId?: string;
  actionType?: string;
  entityName?: string;
  fromUtc?: string;
  toUtc?: string;
  search?: string;
  page?: number;
  pageSize?: number;
};

export type GovernorateOption = {
  governorateId: string;
  name: string;
  isVisibleInJoinForm: boolean;
};

export type CommitteeOption = {
  committeeId: string;
  governorateId: string;
  governorateName: string;
  name: string;
  isVisibleInJoinForm: boolean;
  createdAtUtc: string;
};

export type CommitteeCreateFormState = {
  name: string;
};

export type ImportantContactItem = {
  id: string;
  fullName: string;
  phoneNumber: string;
  positionTitle: string;
  domain: string;
  createdAtUtc: string;
};

export type ImportantContactCreateState = {
  fullName: string;
  phoneNumber: string;
  positionTitle: string;
  domain: string;
};

export type SuggestionStatus = 'Open' | 'Accepted' | 'Rejected';

export type SuggestionItem = {
  id: string;
  title: string;
  description: string;
  status: SuggestionStatus;
  acceptanceCount: number;
  rejectionCount: number;
  currentUserVote: boolean | null;
  createdByMemberName: string;
  createdByMemberRole: Role;
  createdAtUtc: string;
};

export type SuggestionFormState = {
  title: string;
  description: string;
};

export type JoinRequestStatus = 'Pending' | 'Reviewed' | 'Accepted' | 'Rejected';

export type TeamJoinRequest = {
  id: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  nationalId: string | null;
  birthDate: string | null;
  governorateId: string;
  governorateName: string;
  committeeId: string | null;
  committeeName: string | null;
  motivation: string;
  experience: string | null;
  status: JoinRequestStatus | string;
  adminNotes: string | null;
  assignedToMemberId: string | null;
  assignedToMemberName: string | null;
  reviewedByMemberId: string | null;
  reviewedByMemberName: string | null;
  createdAtUtc: string;
  reviewedAtUtc: string | null;
};

export type TeamJoinRequestCreateState = {
  fullName: string;
  email: string;
  phoneNumber: string;
  nationalId: string;
  birthDate: string;
  governorateId: string;
  committeeId: string;
  motivation: string;
  experience: string;
};

export type TeamJoinRequestReviewState = {
  status: 'Accepted' | 'Rejected';
  adminNotes: string;
};

export type NavigationItem = {
  key: SectionKey;
  label: string;
  icon: string;
  roles: Role[];
  permissionKey?: string;
};
