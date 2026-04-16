using BasmaApi.Models;

namespace BasmaApi.Services;

public static class AccessControl
{
    public static bool CanViewAllMembers(Member actor)
    {
        return actor.Role is MemberRole.President or MemberRole.VicePresident;
    }

    public const string ManageUsersPermission = "Users.Manage";
    public const string ManageRolesPermission = "Roles.Manage";
    public const string ManagePointsPermission = "Points.Manage";
    public const string ManageComplaintsPermission = "Complaints.Manage";
    public const string ReviewJoinRequestsPermission = "JoinRequests.Review";
    public const string ViewDashboardPermission = "Dashboard.View";
    public const string CreateCentralMemberPermission = "Members.Create.CentralMember";
    public const string CreateGovernorCoordinatorPermission = "Members.Create.GovernorCoordinator";
    public const string CreateGovernorCommitteeCoordinatorPermission = "Members.Create.GovernorCommitteeCoordinator";
    public const string CreateCommitteeMemberPermission = "Members.Create.CommitteeMember";

    public static bool CanAssignRole(Member actor, MemberRole targetRole)
    {
        if (actor.Role is MemberRole.President or MemberRole.VicePresident)
        {
            return targetRole != MemberRole.President || actor.Role == MemberRole.President;
        }

        return actor.Role == MemberRole.GovernorCoordinator
            ? targetRole is MemberRole.GovernorCommitteeCoordinator or MemberRole.CommitteeMember or MemberRole.CentralMember
            : actor.Role == MemberRole.GovernorCommitteeCoordinator && targetRole == MemberRole.CommitteeMember;
    }

    public static bool CanManageMember(Member actor, Member target)
    {
        if (CanViewAllMembers(actor))
        {
            return true;
        }

        if (actor.Id == target.Id)
        {
            return true;
        }

        return actor.Role switch
        {
            MemberRole.GovernorCoordinator => string.Equals(actor.GovernorName, target.GovernorName, StringComparison.OrdinalIgnoreCase)
                && target.Role is not (MemberRole.President or MemberRole.VicePresident),
            MemberRole.GovernorCommitteeCoordinator => string.Equals(actor.GovernorName, target.GovernorName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(actor.CommitteeName, target.CommitteeName, StringComparison.OrdinalIgnoreCase)
                && target.Role == MemberRole.CommitteeMember,
            MemberRole.CentralMember => target.Role == MemberRole.CommitteeMember,
            _ => false
        };
    }

    public static bool CanCreateMember(Member actor, MemberRole targetRole)
    {
        if (actor.Role is MemberRole.President or MemberRole.VicePresident)
        {
            return true;
        }

        if (actor.Role is MemberRole.CentralMember or MemberRole.CommitteeMember)
        {
            return false;
        }

        var permissionKey = GetCreatePermissionKey(targetRole);
        if (permissionKey is null)
        {
            return false;
        }

        return actor.Role switch
        {
            MemberRole.GovernorCoordinator => targetRole is MemberRole.CentralMember or MemberRole.GovernorCommitteeCoordinator or MemberRole.CommitteeMember && HasPermission(actor, permissionKey),
            MemberRole.GovernorCommitteeCoordinator => targetRole == MemberRole.CommitteeMember && HasPermission(actor, permissionKey),
            MemberRole.CentralMember => targetRole == MemberRole.CommitteeMember && HasPermission(actor, permissionKey),
            _ => false
        };
    }

    public static bool CanManageUsers(Member actor)
    {
        return actor.Role == MemberRole.President || HasPermission(actor, ManageUsersPermission);
    }

    public static bool CanManagePoints(Member actor)
    {
        return actor.Role == MemberRole.President || HasPermission(actor, ManagePointsPermission);
    }

    public static bool CanManageComplaints(Member actor)
    {
        return actor.Role == MemberRole.President || HasPermission(actor, ManageComplaintsPermission);
    }

    public static bool CanReviewJoinRequests(Member actor)
    {
        return actor.Role == MemberRole.President || HasPermission(actor, ReviewJoinRequestsPermission);
    }

    public static bool CanManageCommitteeCatalog(Member actor)
    {
        return actor.Role is MemberRole.President or MemberRole.VicePresident or MemberRole.GovernorCoordinator;
    }

    public static bool CanViewAuditLogs(Member actor)
    {
        return actor.Role == MemberRole.President;
    }

    public static bool CanEscalateComplaint(Member actor, Complaint complaint)
    {
        if (actor.Role is MemberRole.President or MemberRole.VicePresident)
        {
            return complaint.Status != ComplaintStatus.Resolved;
        }

        return actor.Role switch
        {
            MemberRole.GovernorCoordinator => complaint.EscalationLevel <= 0 && complaint.Status != ComplaintStatus.Resolved,
            MemberRole.GovernorCommitteeCoordinator => false,
            MemberRole.CentralMember => false,
            _ => false
        };
    }

    public static bool CanCommentOnComplaint(Member actor, Complaint complaint)
    {
        return actor.Role is MemberRole.President or MemberRole.VicePresident
            || CanManageComplaints(actor)
            || actor.Id == complaint.MemberId;
    }

    public static bool CanViewDashboard(Member actor)
    {
        return HasPermission(actor, ViewDashboardPermission) || actor is not null;
    }

    public static bool HasPermission(Member actor, string permissionKey)
    {
        return actor.PermissionGrants.Any(grant => string.Equals(grant.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> DefaultPermissionsForRole(MemberRole role)
    {
        return role switch
        {
            MemberRole.President => [ManageUsersPermission, ManageRolesPermission, ManagePointsPermission, ManageComplaintsPermission, ReviewJoinRequestsPermission, ViewDashboardPermission, CreateCentralMemberPermission, CreateGovernorCoordinatorPermission, CreateGovernorCommitteeCoordinatorPermission, CreateCommitteeMemberPermission],
            MemberRole.VicePresident => [ManageUsersPermission, ManageRolesPermission, ManagePointsPermission, ManageComplaintsPermission, ViewDashboardPermission, CreateCentralMemberPermission, CreateGovernorCoordinatorPermission, CreateGovernorCommitteeCoordinatorPermission, CreateCommitteeMemberPermission],
            MemberRole.CentralMember => [ManageUsersPermission, ManageComplaintsPermission, ViewDashboardPermission],
            MemberRole.GovernorCoordinator => [ManageUsersPermission, ReviewJoinRequestsPermission, ViewDashboardPermission, CreateCentralMemberPermission, CreateGovernorCommitteeCoordinatorPermission, CreateCommitteeMemberPermission],
            MemberRole.GovernorCommitteeCoordinator => [ManageUsersPermission, ViewDashboardPermission, CreateCommitteeMemberPermission],
            _ => [ViewDashboardPermission]
        };
    }

    private static string? GetCreatePermissionKey(MemberRole targetRole)
    {
        return targetRole switch
        {
            MemberRole.CentralMember => CreateCentralMemberPermission,
            MemberRole.GovernorCoordinator => CreateGovernorCoordinatorPermission,
            MemberRole.GovernorCommitteeCoordinator => CreateGovernorCommitteeCoordinatorPermission,
            MemberRole.CommitteeMember => CreateCommitteeMemberPermission,
            _ => null
        };
    }

    public static IReadOnlyList<string> GetEffectivePermissions(Member member)
    {
        return DefaultPermissionsForRole(member.Role)
            .Concat(member.PermissionGrants.Select(grant => grant.PermissionKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
