using Android.App;
using Android.Runtime;
using Avalonia.Android;

namespace ValleyKeepsake.Android;

[Application]
public class AndroidApplication : AvaloniaAndroidApplication<App>
{
    protected AndroidApplication(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }
}
