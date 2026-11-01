using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows.Media.Imaging;

namespace Falak.Engines;

// Read-only EXIF/WPF metadata export. Existing strip-metadata feature is unchanged.
internal static class ImageMetadataEngine
{
    public static JobResult Export(IReadOnlyList<string> inputs,IProgress<double>? progress,CancellationToken ct)
    {
        var results=new List<string>();
        int done=0;
        foreach(var input in inputs)
        {
            ct.ThrowIfCancellationRequested();
            BitmapSource image=ImageEngine.Load(input);
            string output=OutputPaths.NextTo(input,".json","metadata");
            var data=new Dictionary<string,object?>
            {
                ["FileName"]=Path.GetFileName(input),
                ["Width"]=image.PixelWidth,
                ["Height"]=image.PixelHeight,
                ["DpiX"]=image.DpiX,
                ["DpiY"]=image.DpiY,
                ["PixelFormat"]=image.Format.ToString()
            };
            try {
                using var stream=File.OpenRead(input);
                var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
                if(frame.Metadata is BitmapMetadata info)
                {
                    data["DateTaken"]=info.DateTaken;
                    data["CameraManufacturer"]=info.CameraManufacturer;
                    data["CameraModel"]=info.CameraModel;
                    data["ApplicationName"]=info.ApplicationName;
                    data["Title"]=info.Title;
                    data["Comment"]=info.Comment;
                }
            } catch (NotSupportedException) { }
            File.WriteAllText(output,JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
            results.Add(output);progress?.Report((double)++done/inputs.Count);
        }
        return new JobResult(results,null,"تم تصدير بيانات الصورة");
    }
}
