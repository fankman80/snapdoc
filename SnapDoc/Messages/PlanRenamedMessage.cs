using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SnapDoc.Messages;

public record PlanRenamedMessage((string PlanId, string NewName) Value);