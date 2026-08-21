using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SnapDoc.Messages;

public class PinAddedMessage((string PlanId, string PinId) value) : ValueChangedMessage<(string PlanId, string PinId)>(value)
{
}