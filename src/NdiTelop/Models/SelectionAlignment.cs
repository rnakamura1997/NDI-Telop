namespace NdiTelop.Models;

public enum SelectionAlignmentReferenceMode
{
    SelectionBounds,
    LastSelectedElement
}

public enum SelectionAlignmentCommand
{
    AlignLeft,
    AlignHorizontalCenter,
    AlignRight,
    AlignTop,
    AlignVerticalCenter,
    AlignBottom,
    DistributeHorizontal,
    DistributeVertical
}
