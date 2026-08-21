using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SnapDoc.Messages;

public enum RemoteChangeType
{
    ProjectDetailsUpdated,
    PlanListUpdated,
    PinsUpdated
}

public class RemoteDataChangedMessage(RemoteChangeType changeType) : ValueChangedMessage<RemoteChangeType>(changeType);