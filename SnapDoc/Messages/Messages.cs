using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SnapDoc.Messages;

public class PinChangedMessage(string pinId) : ValueChangedMessage<string>(pinId)
{
}

public class PinAddedMessage((string PlanId, string PinId) value) : ValueChangedMessage<(string PlanId, string PinId)>(value)
{
}

public class PinDeletedMessage(string pinId) : ValueChangedMessage<string>(pinId)
{
}

public class PinPropertyChangedMessage(string pinId, bool isLockPosition) : ValueChangedMessage<(string PinId, bool IsLockPosition)>((pinId, isLockPosition))
{
}

public class PlanDetailsChangedMessage((string PlanId, string Name, string Description, bool IsGrayscale, string PlanColor) value) : ValueChangedMessage<(string PlanId, string Name, string Description, bool IsGrayscale, string PlanColor)>(value)
{
}

public class RemoteDataChangedMessage(RemoteChangeType changeType) : ValueChangedMessage<RemoteChangeType>(changeType);

public class ResetTouchesMessage
{ 
}

public class TitleImageChangedMessage(string oldFileName, string newFileName)
{
    public string OldFileName { get; } = oldFileName;
    public string NewFileName { get; } = newFileName;
}