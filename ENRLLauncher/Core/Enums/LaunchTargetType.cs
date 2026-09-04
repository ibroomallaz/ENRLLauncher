namespace ENRLLauncher.Core.Enums;

public enum LaunchTargetType
{
    Presentation, // .pptx, .ppt, .ppsx (kiosk/slideshow)
    WebLink,      // HTTP/HTTPS URLs
    Application,  // .exe, .bat, .cmd
    Document,      // PDF, Word, or general file association
    HorizontalSeparator, // Full row break
    LongVerticalSeparator, // Long column break
    ShortVerticalSeparator, // Short column break
}