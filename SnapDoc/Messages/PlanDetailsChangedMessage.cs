using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SnapDoc.Messages;

public class PlanDetailsChangedMessage : ValueChangedMessage<(string PlanId, string Name, string Description, bool IsGrayscale, string PlanColor)>
{
    public PlanDetailsChangedMessage((string PlanId, string Name, string Description, bool IsGrayscale, string PlanColor) value) : base(value) { }
}