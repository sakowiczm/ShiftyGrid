using static ABI.System.Collections.Generic.IReadOnlyDictionary_Delegates;

namespace ShiftyGrid.Commands
{
    internal class ResizeCommand
    {
        // todo:
        //  - consider different name
        //  
        //  Resize foreground window left or right. If adjecent window detected (not overlaping, in x pixel range) -
        //  this window also changes it size (increase / decrease). Window cannot be resized beyond monitor boundary.
        //  Resize is only within current monitor window is in.
        //  
        //  If we have 2 adjecent resized windows - we can reset the split each window get's it's half of the screen.

        //  Resize Left     Alt + Left Arrow
        //  Resize Right    Alt +Right Arrow
        //  Reset Split     Alt + =
    }
}
