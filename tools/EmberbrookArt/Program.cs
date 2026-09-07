using SkiaSharp;

string destination = args.Length > 0 ? args[0] : "GameEngine.Demo/assets/images/emberbrook";
Directory.CreateDirectory(destination);
var drawings = new Dictionary<string, Action<SKCanvas>>();
void Asset(string name, Action<SKCanvas> draw)
{
    drawings[name] = draw;
    using var bitmap = new SKBitmap(40, 40);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);
    draw(canvas);
    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    using var stream = File.Create(Path.Combine(destination, name + ".png"));
    data.SaveTo(stream);
}
void Box(SKCanvas c, float x, float y, float w, float h, string color)
{
    using var paint = new SKPaint { Color = SKColor.Parse(color), IsAntialias = false };
    c.DrawRect(x, y, w, h, paint);
}
void Oval(SKCanvas c, float x, float y, float w, float h, string color)
{
    using var paint = new SKPaint { Color = SKColor.Parse(color), IsAntialias = false };
    c.DrawOval(new SKRect(x, y, x + w, y + h), paint);
}
void Poly(SKCanvas c, string color, params float[] points)
{
    using var path = new SKPathBuilder();
    path.MoveTo(points[0], points[1]);
    for (int i = 2; i < points.Length; i += 2) path.LineTo(points[i], points[i + 1]);
    path.Close();
    using var paint = new SKPaint { Color = SKColor.Parse(color), IsAntialias = false };
    using var shape = path.Detach();
    c.DrawPath(shape, paint);
}
Asset("panel", c => Box(c, 0, 0, 40, 40, "#202b29"));
Asset("paper", c => Box(c, 0, 0, 40, 40, "#303a32"));
Asset("button", c => { Box(c, 0, 0, 40, 40, "#a08451"); Box(c, 1, 1, 38, 38, "#354839"); });
for (int variant = 0; variant < 3; variant++)
{
    int seed = variant;
    Asset("grass" + variant, c =>
    {
        Box(c, 0, 0, 40, 40, new[] { "#667c47", "#627947", "#6b804b" }[seed]);
        var random = new Random(seed + 91);
        for (int i = 0; i < 14; i++)
        {
            int x = random.Next(2, 37), y = random.Next(2, 37);
            Box(c, x, y, 2, 2, i % 3 == 0 ? "#8b985b" : "#526b3c");
            if (i % 4 == 0) Box(c, x + 2, y - 2, 1, 3, "#8b985b");
        }
    });
}
Asset("path", c =>
{
    Box(c, 0, 0, 40, 40, "#a99870");
    for (int i = 0; i < 5; i++)
    {
        int x = i * 13 % 33, y = i * 19 % 34;
        Box(c, x, y, 6, 3, "#958561"); Box(c, x, y, 4, 1, "#c0ac7d");
    }
});
Asset("water", c =>
{
    Box(c, 0, 0, 40, 40, "#365f66");
    for (int i = 0; i < 4; i++)
    {
        Box(c, i * 13 % 25, 5 + i * 9, 13, 2, "#548087");
        Box(c, i * 7 % 20, 8 + i * 9, 8, 1, "#709c99");
    }
});
Asset("bridge", c =>
{
    Box(c, 0, 0, 40, 40, "#594c38");
    for (int x = 1; x < 40; x += 8) { Box(c, x, 0, 6, 40, "#b69a65"); Box(c, x, 0, 1, 40, "#cfb77d"); }
    Box(c, 0, 1, 40, 3, "#745733"); Box(c, 0, 36, 40, 3, "#745733");
});
Asset("tree", c =>
{
    Oval(c, 3, 28, 34, 11, "#465a35"); Box(c, 17, 18, 7, 18, "#675039"); Box(c, 18, 20, 2, 16, "#97794b");
    Poly(c, "#294d3a", 2, 27, 9, 12, 17, 3, 25, 4, 37, 24, 33, 30, 8, 32);
    Poly(c, "#3e6642", 5, 20, 17, 3, 24, 4, 31, 19, 20, 24);
    Poly(c, "#638044", 10, 14, 18, 3, 24, 5, 26, 13, 17, 17);
});
Asset("ore", c =>
{
    Oval(c, 3, 28, 34, 10, "#47573b");
    Poly(c, "#545c59", 3, 29, 8, 15, 19, 8, 30, 13, 37, 29, 30, 35, 10, 35);
    Poly(c, "#89918a", 8, 15, 19, 8, 24, 19, 13, 25, 3, 29);
    Poly(c, "#69766b", 24, 19, 30, 13, 37, 29, 30, 35, 19, 28);
    Box(c, 11, 18, 6, 4, "#cf915b"); Box(c, 25, 24, 7, 5, "#ac7049"); Box(c, 26, 24, 5, 2, "#efb575");
});
Asset("forge", c =>
{
    Box(c, 4, 15, 32, 22, "#4e5047"); Box(c, 8, 6, 10, 12, "#656958");
    Box(c, 6, 3, 14, 5, "#8e8d72"); Box(c, 7, 18, 26, 17, "#92907a");
    Box(c, 11, 21, 18, 14, "#332d2b"); Box(c, 13, 27, 14, 6, "#d0743f");
    Poly(c, "#f0bd62", 15, 32, 15, 25, 19, 28, 23, 22, 25, 32);
    Box(c, 3, 35, 34, 3, "#b4a486"); Box(c, 24, 12, 12, 4, "#b5bca5");
});
Asset("bank", c =>
{
    Oval(c, 3, 30, 34, 8, "#465a35"); Box(c, 5, 12, 30, 22, "#5a422f");
    Box(c, 7, 10, 26, 14, "#a77943"); Box(c, 7, 25, 26, 7, "#855932");
    Box(c, 10, 10, 3, 24, "#cfb778"); Box(c, 28, 10, 3, 24, "#cfb778");
    Box(c, 18, 21, 6, 7, "#e1c984"); Box(c, 20, 23, 2, 3, "#514732");
});
void Person(SKCanvas c, string coat, string hair, bool sword)
{
    Oval(c, 8, 31, 24, 7, "#425a3a");
    Box(c, 12, 28, 6, 8, "#463e33"); Box(c, 23, 28, 6, 8, "#463e33");
    Box(c, 10, 17, 21, 14, coat); Box(c, 8, 20, 4, 10, "#d9b587"); Box(c, 30, 20, 4, 10, "#d9b587");
    Box(c, 13, 7, 15, 13, "#dab98b"); Box(c, 12, 4, 17, 7, hair); Box(c, 12, 7, 4, 8, hair);
    Box(c, 17, 12, 2, 2, "#343c33"); Box(c, 24, 12, 2, 2, "#343c33");
    Box(c, 12, 27, 18, 3, "#766044"); Box(c, 19, 27, 4, 3, "#c4b16c");
    if (sword) { Box(c, 34, 12, 3, 15, "#ced4bd"); Box(c, 31, 26, 8, 2, "#c6a265"); Box(c, 34, 28, 3, 5, "#69513a"); }
}
Asset("player", c => Person(c, "#426881", "#544a36", true));
Asset("elder", c => { Person(c, "#8b6450", "#d3c6a2", false); Box(c, 34, 14, 2, 23, "#795435"); });
Asset("merchant", c => { Person(c, "#687448", "#745332", false); Box(c, 12, 4, 18, 4, "#b39d5c"); });
Asset("mossling", c =>
{
    Oval(c, 5, 29, 30, 8, "#425a3a");
    Poly(c, "#46664a", 7, 31, 4, 19, 12, 10, 28, 10, 35, 20, 31, 33);
    Poly(c, "#86a561", 6, 19, 12, 10, 28, 10, 33, 19, 24, 24, 13, 23);
    Box(c, 12, 20, 5, 5, "#e7d69c"); Box(c, 25, 20, 5, 5, "#e7d69c");
    Box(c, 14, 21, 2, 3, "#3a4032"); Box(c, 25, 21, 2, 3, "#3a4032");
    Box(c, 17, 29, 8, 2, "#2c493c"); Poly(c, "#a2b875", 12, 10, 10, 3, 19, 10); Poly(c, "#a2b875", 25, 10, 31, 4, 29, 15);
});
Asset("target", c =>
{
    foreach (int x in new[] { 1, 32 }) foreach (int y in new[] { 1, 32 })
    { Box(c, x, y, 7, 2, "#f5d486"); Box(c, x, y, 2, 7, "#f5d486"); }
});

Asset("cottage", c =>
{
    Box(c, 4, 17, 32, 21, "#bda779"); Box(c, 4, 17, 3, 21, "#6c533d");
    Box(c, 32, 17, 4, 21, "#6c533d"); Box(c, 4, 34, 32, 4, "#70624b");
    Poly(c, "#684537", 0, 20, 5, 3, 34, 3, 40, 20);
    Poly(c, "#986142", 2, 17, 6, 3, 33, 3, 38, 17);
    for (int y = 6; y < 18; y += 4) Box(c, 6, y, 28, 1, "#b57b4f");
    Box(c, 17, 23, 8, 15, "#574b37"); Box(c, 19, 25, 4, 13, "#7c6645");
    Box(c, 8, 24, 6, 7, "#526767"); Box(c, 9, 25, 4, 4, "#d9c886");
    Box(c, 28, 24, 4, 7, "#d9c886"); Box(c, 28, 0, 5, 9, "#85806b");
});
Asset("floor", c =>
{
    Box(c, 0, 0, 40, 40, "#434c48");
    Box(c, 1, 1, 18, 18, "#566058"); Box(c, 21, 1, 18, 18, "#505c55");
    Box(c, 1, 21, 18, 18, "#515950"); Box(c, 21, 21, 18, 18, "#576057");
    Box(c, 3, 2, 12, 1, "#6e7565"); Box(c, 26, 34, 7, 2, "#687952");
});
Asset("wall", c =>
{
    Box(c, 0, 0, 40, 40, "#2b3837");
    for (int y = 0; y < 40; y += 10)
    {
        Box(c, 1, y + 1, 38, 8, "#657165");
        Box(c, y % 20 == 0 ? 13 : 27, y, 2, 10, "#354740");
        Box(c, 2, y + 1, 10, 1, "#8b9177");
    }
});
Asset("gate", c =>
{
    Box(c, 3, 6, 34, 33, "#626e60"); Box(c, 10, 8, 20, 31, "#263838");
    Box(c, 6, 2, 28, 6, "#a0a085"); Box(c, 3, 12, 7, 3, "#a0a085");
    Box(c, 30, 20, 7, 3, "#a0a085"); Box(c, 17, 24, 6, 14, "#65a39b");
    Box(c, 13, 29, 14, 4, "#b4d7af");
});
Asset("sentinel", c =>
{
    Oval(c, 5, 31, 30, 7, "#293d36");
    Box(c, 10, 16, 21, 17, "#758676"); Box(c, 13, 5, 15, 13, "#9d9e80");
    Box(c, 16, 11, 3, 3, "#e1ba69"); Box(c, 24, 11, 3, 3, "#e1ba69");
    Box(c, 11, 20, 18, 3, "#466b5a"); Box(c, 12, 31, 6, 6, "#57685d");
    Box(c, 24, 31, 6, 6, "#57685d"); Box(c, 34, 5, 2, 30, "#b5ae83");
    Poly(c, "#c4d0ac", 31, 8, 35, 0, 39, 8);
});
Asset("guardian", c =>
{
    Oval(c, 1, 32, 38, 7, "#273733"); Box(c, 7, 13, 27, 20, "#87907a");
    Box(c, 2, 15, 8, 17, "#596f64"); Box(c, 32, 15, 7, 17, "#596f64");
    Box(c, 12, 4, 17, 13, "#b6b293"); Box(c, 15, 10, 4, 3, "#f2bc5f");
    Box(c, 24, 10, 4, 3, "#f2bc5f"); Box(c, 13, 20, 14, 10, "#344c46");
    Oval(c, 16, 21, 9, 9, "#e7b865"); Box(c, 10, 32, 8, 6, "#a5a589");
    Box(c, 24, 32, 8, 6, "#a5a589");
});
Asset("relic", c =>
{
    Box(c, 6, 30, 28, 8, "#757f70"); Box(c, 10, 27, 20, 4, "#b1b091");
    Poly(c, "#bd974e", 11, 25, 14, 18, 14, 10, 19, 6, 23, 7, 27, 11, 27, 18, 30, 25);
    Box(c, 12, 23, 17, 3, "#f1d187"); Box(c, 18, 10, 3, 11, "#e3bf6b");
    Box(c, 19, 27, 4, 3, "#dbbc6e");
});
Asset("danger", c =>
{
    Box(c, 0, 0, 40, 40, "#A0D67A38");
    Box(c, 1, 1, 38, 2, "#ffd989"); Box(c, 1, 37, 38, 2, "#ffd989");
    Box(c, 1, 1, 2, 38, "#ffd989"); Box(c, 37, 1, 2, 38, "#ffd989");
    Poly(c, "#ffe8ac", 20, 8, 32, 29, 8, 29);
    Box(c, 19, 15, 2, 7, "#83552e"); Box(c, 19, 25, 2, 2, "#83552e");
});
Asset("fishing", c =>
{
    Oval(c, 3, 18, 34, 14, "#709c99"); Oval(c, 5, 20, 30, 10, "#365f66");
    Oval(c, 8, 16, 16, 8, "#d4d9b5"); Poly(c, "#8fbeac", 21, 19, 30, 13, 29, 25);
    Box(c, 10, 18, 2, 2, "#2c494a"); Box(c, 12, 16, 8, 2, "#97bca6");
    Box(c, 7, 7, 3, 4, "#d9e8cc"); Box(c, 26, 5, 2, 3, "#a6ccc0");
});
Asset("campfire", c =>
{
    Oval(c, 2, 27, 36, 12, "#455a39"); Oval(c, 5, 26, 30, 11, "#8d8f74");
    Oval(c, 9, 28, 22, 7, "#4d4134");
    Poly(c, "#896038", 9, 29, 11, 26, 31, 32, 29, 36);
    Poly(c, "#ac7844", 9, 32, 29, 26, 31, 29, 11, 36);
    Poly(c, "#dc8443", 10, 29, 14, 17, 18, 22, 23, 7, 30, 24, 27, 31);
    Poly(c, "#f5c968", 16, 29, 19, 21, 22, 23, 25, 18, 26, 29);
    Box(c, 3, 9, 2, 22, "#605441"); Box(c, 35, 9, 2, 22, "#605441");
    Box(c, 3, 9, 34, 2, "#605441");
});
Asset("ash", c =>
{
    Oval(c, 3, 29, 34, 9, "#435b35"); Box(c, 16, 9, 8, 28, "#a49a78");
    Box(c, 17, 13, 2, 23, "#dad0a3"); Box(c, 20, 18, 4, 2, "#706a52");
    Poly(c, "#91a368", 2, 19, 8, 6, 20, 1, 31, 5, 38, 19, 29, 26, 10, 25);
    Poly(c, "#b8bc7b", 8, 13, 20, 1, 31, 5, 29, 15, 17, 21);
});
Asset("beacon", c =>
{
    Oval(c, 2, 30, 36, 8, "#405736"); Box(c, 9, 29, 22, 7, "#85816a");
    Box(c, 12, 14, 16, 16, "#635b48"); Box(c, 8, 12, 24, 5, "#b6a477");
    Box(c, 13, 17, 3, 11, "#a48f65"); Box(c, 22, 17, 3, 11, "#a48f65");
    Box(c, 10, 7, 4, 9, "#807051"); Box(c, 27, 7, 4, 9, "#807051");
});
Asset("wolf", c =>
{
    Oval(c, 2, 30, 36, 8, "#3d5336");
    Poly(c, "#888b77", 3, 25, 9, 12, 27, 14, 37, 23, 31, 31, 11, 31);
    Poly(c, "#b2b09a", 5, 22, 6, 7, 14, 15, 23, 15, 31, 7, 33, 24, 20, 33);
    Box(c, 10, 21, 4, 3, "#e6c075"); Box(c, 26, 21, 4, 3, "#e6c075");
    Box(c, 17, 27, 7, 4, "#414c3e"); Box(c, 9, 31, 5, 6, "#595e4d"); Box(c, 28, 31, 5, 6, "#595e4d");
});
Asset("hart", c =>
{
    Oval(c, 2, 32, 36, 7, "#3a5033"); Box(c, 10, 20, 22, 13, "#8c7652");
    Box(c, 12, 31, 5, 7, "#b3a178"); Box(c, 26, 31, 5, 7, "#b3a178");
    Poly(c, "#baa47b", 13, 14, 18, 10, 29, 14, 27, 27, 19, 30, 14, 24);
    Box(c, 16, 18, 3, 3, "#e4db86"); Box(c, 25, 18, 3, 3, "#e4db86");
    Box(c, 12, 2, 3, 13, "#ccc4a0"); Box(c, 27, 2, 3, 13, "#ccc4a0");
    Box(c, 5, 7, 9, 3, "#ccc4a0"); Box(c, 5, 1, 3, 9, "#ccc4a0");
    Box(c, 29, 7, 8, 3, "#ccc4a0"); Box(c, 34, 1, 3, 9, "#ccc4a0");
    Box(c, 19, 7, 3, 8, "#6e8d4c");
});

Asset("health", c => Box(c, 0, 0, 40, 40, "#a6ce84"));
Asset("wound", c => Box(c, 0, 0, 40, 40, "#ed806b"));
Asset("progress", c => Box(c, 0, 0, 40, 40, "#d8b76a"));

foreach (string direction in new[] { "up", "down", "left", "right" })
foreach (string pose in new[] { "idle", "work" })
{
    Asset("player" + direction + pose, c =>
    {
        bool work = pose == "work";
        Oval(c, 8, 32, 25, 6, "#334d39");
        Box(c, 12, 27, 6, work ? 9 : 8, "#353a3b");
        Box(c, 23, work ? 29 : 27, 6, work ? 7 : 8, "#353a3b");
        Box(c, 10, 16, 21, 15, "#426c86");
        Box(c, 13, 5, 15, 13, "#dcb47d");
        Box(c, 12, 4, 17, 5, "#624834");
        if (direction == "up") Box(c, 12, 7, 17, 10, "#624834");
        else if (direction == "left") Box(c, 13, 10, 3, 3, "#202b29");
        else if (direction == "right") Box(c, 25, 10, 3, 3, "#202b29");
        else { Box(c, 15, 10, 3, 3, "#202b29"); Box(c, 23, 10, 3, 3, "#202b29"); }
        Box(c, work ? 30 : 7, work ? 11 : 19, 5, 10, "#dcb47d");
        Box(c, 10, 27, 21, 3, "#ad8750");
        if (work) { Box(c, 33, 4, 3, 15, "#c5d3cc"); Box(c, 30, 17, 9, 3, "#d8b76a"); }
    });
}
Asset("itemore", c => { Poly(c, "#9e6747", 5,30, 9,13, 22,6, 35,20, 31,34); Box(c, 14, 14, 8, 8, "#dbb477"); });
Asset("itembar", c => { Poly(c, "#b98256", 4,28, 10,14, 31,14, 37,28); Box(c, 5,28,31,6,"#805137"); });
Asset("itemlog", c => { Box(c,6,14,27,14,"#795338"); Oval(c,24,13,13,16,"#d2b078"); Box(c,8,17,17,3,"#ad8151"); });
Asset("itemtrout", c => { Oval(c,7,12,25,16,"#a2c6bf"); Poly(c,"#708f9c", 9,20, 2,9, 2,31); Box(c,26,17,3,3,"#263b3a"); });
Asset("itemmeal", c => { Oval(c,3,24,34,10,"#d8c894"); Oval(c,7,12,25,15,"#ce954c"); Box(c,16,14,2,10,"#8d5734"); Box(c,23,14,2,10,"#8d5734"); });
Asset("itemsmoked", c => { Oval(c,5,17,29,15,"#8d5734"); Box(c,14,8,3,8,"#c6c2a0"); Box(c,23,4,3,12,"#c6c2a0"); });
Asset("itemration", c => { Box(c,9,13,23,20,"#a88956"); Box(c,7,12,27,6,"#d6bd7a"); Box(c,18,12,4,21,"#6c7350"); });
Asset("feast", c => { Box(c,3,12,34,18,"#986f45"); Box(c,6,29,5,9,"#664631"); Box(c,29,29,5,9,"#664631"); Oval(c,6,15,12,8,"#e9d4a0"); Oval(c,22,16,11,8,"#d39a56"); Box(c,19,8,4,9,"#98b5a0"); });
string soundDirectory = Path.GetFullPath(Path.Combine(destination, "..", "..", "sounds", "emberbrook"));
Directory.CreateDirectory(soundDirectory);
foreach (var sound in new[] { ("Select", 520.0, 0.06), ("Gather", 340.0, 0.12), ("Craft", 660.0, 0.2), ("Damage", 150.0, 0.14), ("Danger", 880.0, 0.35), ("Complete", 523.25, 0.55) })
{
    int samples = (int)(22050 * sound.Item3);
    using var writer = new BinaryWriter(File.Create(Path.Combine(soundDirectory, sound.Item1 + ".wav")));
    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
    writer.Write(22050); writer.Write(44100); writer.Write((short)2); writer.Write((short)16);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
    for (int i = 0; i < samples; i++)
    {
        double t = i / 22050.0, envelope = Math.Min(1, t * 100) * (1 - (double)i / samples);
        double frequency = sound.Item1 == "Complete" ? sound.Item2 * (i < samples / 3 ? 1 : i < samples * 2 / 3 ? 1.25 : 1.5) : sound.Item2;
        writer.Write((short)(Math.Sin(t * frequency * Math.PI * 2) * envelope * 5000));
    }
}
Asset("brokenbridge", c => { Box(c, 0, 6, 10, 5, "#806043"); Box(c, 30, 6, 10, 5, "#806043"); Box(c, 0, 30, 10, 5, "#806043"); Box(c, 30, 30, 10, 5, "#806043"); Box(c, 3, 8, 5, 24, "#ac8c58"); Box(c, 32, 8, 5, 24, "#ac8c58"); });

void Strip(string name, Action<SKCanvas, int> draw)
{
    using var bitmap = new SKBitmap(160, 40);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);
    for (int frame = 0; frame < 4; frame++)
    {
        canvas.Save(); canvas.ClipRect(new SKRect(frame * 40, 0, frame * 40 + 40, 40));
        canvas.Translate(frame * 40, 0); draw(canvas, frame); canvas.Restore();
    }
    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    using var stream = File.Create(Path.Combine(destination, name + ".png"));
    data.SaveTo(stream);
}
Strip("watermotion", (c, f) =>
{
    Box(c, 0, 0, 40, 40, "#365f66");
    for (int row = 0; row < 5; row++)
    for (int wrap = -1; wrap <= 1; wrap++)
    {
        int x = (row * 13 + f * 3) % 40 + wrap * 40;
        Box(c, x, row * 9 + 2, 12, 2, "#548087");
        Box(c, x + 3, row * 9 + 5, 6, 1, "#709c99");
    }
});
foreach (string name in new[] { "tree", "ash" })
    Strip(name + "motion", (c, f) =>
    {
        c.Save(); c.ClipRect(new SKRect(0, 28, 40, 40)); drawings[name](c); c.Restore();
        c.Save(); c.ClipRect(new SKRect(0, 0, 40, 28)); c.Translate(f == 1 ? 1 : f == 3 ? -1 : 0, 0); drawings[name](c); c.Restore();
    });
foreach (string name in new[] { "mossling", "wolf", "sentinel", "guardian", "hart", "elder", "merchant" })
    Strip(name + "motion", (c, f) =>
    {
        c.Save(); c.ClipRect(new SKRect(0, 32, 40, 40)); drawings[name](c); c.Restore();
        c.Save(); c.ClipRect(new SKRect(0, 0, 40, 32));
        c.Translate(0, f == 1 ? -1 : f == 3 ? 1 : 0); drawings[name](c); c.Restore();
        if (name is "elder" or "merchant" && f == 3) { Box(c, 17, 12, 2, 2, "#dab98b"); Box(c, 24, 12, 2, 2, "#dab98b"); }
    });
Strip("fishingmotion", (c, f) =>
{
    Oval(c, 3 - f / 2f, 18 - f, 34 + f, 14 + f, "#709c99");
    Oval(c, 5, 20 - f, 30, 10 + f, "#365f66");
    c.Save(); c.Translate(f == 1 ? 2 : f == 3 ? -2 : 0, f % 2);
    Oval(c, 8, 16, 16, 8, "#d4d9b5"); Poly(c, "#8fbeac", 21, 19, 30, 13 + f, 29, 25 - f);
    Box(c, 10, 18, 2, 2, "#2c494a"); c.Restore();
});
Strip("campfiremotion", (c, f) =>
{
    c.Save(); c.ClipRect(new SKRect(0, 26, 40, 40)); drawings["campfire"](c); c.Restore();
    Poly(c, "#dc8443", 10,29, 13+f,16, 18,22, 23-f,5+f*2, 30,24, 27,31);
    Poly(c, "#f5c968", 16,29, 19,20-f, 22,23, 25,17+f, 26,29);
    Box(c, 3, 9, 2, 22, "#605441"); Box(c, 35, 9, 2, 22, "#605441"); Box(c, 3, 9, 34, 2, "#605441");
    Box(c, 13 + f * 3, 14 - f * 4, 2, 2, "#ffe39a");
});
Strip("forgemotion", (c, f) =>
{
    drawings["forge"](c); Box(c, 11, 21, 18, 14, "#332d2b");
    Poly(c, "#d0743f", 12,34, 14,25+f, 20,30, 24,22+f, 29,34);
    Poly(c, "#f0bd62", 16,34, 20,27-f, 25,34);
});
foreach (string direction in new[] { "up", "down", "left", "right" })
foreach (string action in new[] { "idle", "walk", "mine", "chop", "fish", "cook", "attack", "spear" })
    Strip("hero" + direction + action, (c, f) =>
    {
        int stride = action == "walk" ? new[] { 0, 2, 0, -2 }[f] : 0;
        int bob = action == "walk" && f % 2 == 1 ? -1 : 0;
        Oval(c, 8, 32, 25, 6, "#334d39");
        Box(c, 12, 27 + stride, 6, 8 - stride, "#353a3b"); Box(c, 23, 27 - stride, 6, 8 + stride, "#353a3b");
        c.Save(); c.Translate(0, bob);
        Box(c, 10, 17, 21, 14, "#426c86"); Box(c, 13, 5, 15, 13, "#dcb47d"); Box(c, 12, 4, 17, 5, "#624834");
        if (direction == "up") Box(c, 12, 7, 17, 10, "#624834");
        else if (f != 3 || action != "idle")
        {
            if (direction != "right") Box(c, 15, 10, 2, 2, "#202b29");
            if (direction != "left") Box(c, 25, 10, 2, 2, "#202b29");
        }
        Box(c, 7, 19 - stride, 5, 10, "#dcb47d"); Box(c, 30, 19 + stride, 4, 10, "#dcb47d");
        Box(c, 10, 27, 21, 3, "#ad8750");
        if (action is "mine" or "chop" or "attack" or "spear")
        {
            c.Save();
            if (direction == "left") { c.Translate(40, 0); c.Scale(-1, 1); }
            c.Translate(31, 24); c.RotateDegrees(new[] { -35f, -65f, 25f, 5f }[f]);
            Box(c, 0, -15, 3, 20, "#a88956");
            if (action == "mine") Box(c, -5, -16, 13, 4, "#b7c7c3");
            else if (action == "chop") Poly(c, "#b7c7c3", 0,-17, 8,-16, 8,-8, 0,-10);
            else if (action == "spear") Poly(c, "#b7c7c3", -2,-15, 1,-23, 5,-15);
            else { Box(c, 0, -19, 3, 19, "#c5d3cc"); Box(c, -3, 0, 9, 2, "#d8b76a"); }
            c.Restore();
        }
        if (action == "fish")
        {
            Poly(c, "#b79762", 31,29, 32,29, 37,5, 35,5);
            Box(c, 36, 5, 1, 18 + f, "#ddd4ac"); Box(c, 35, 22 + f, 3, 3, "#dc8443");
        }
        if (action == "cook")
        {
            Oval(c, 23, 22, 16, 9, "#4b5050"); Oval(c, 26, 24, 10, 5, "#ce954c");
            Box(c, 27 + f, 15 - f, 2, 4, "#d4dcc3");
        }
        c.Restore();
    });
foreach (string name in new[] { "spark", "splash", "strike" })
    Strip(name, (c, f) =>
    {
        if (name == "strike")
        {
            Poly(c, f < 2 ? "#fff0c3" : "#d79968", 5+f*2,4, 9+f*2,5, 35,31-f*2, 30,31-f*2);
            Box(c, 8, 25-f*4, 3, 3, "#ed806b");
        }
        else
        {
            string color = name == "spark" ? "#ffe39a" : "#b5e1d4";
            for (int i = 0; i < 5; i++)
            {
                double angle = i * Math.PI * 2 / 5;
                Box(c, 20 + (float)Math.Cos(angle) * (5 + f * 4), 22 + (float)Math.Sin(angle) * (5 + f * 3) - f * 2, 3-f/2, 3-f/2, color);
            }
        }
    });
foreach (string name in new[] { "guardian", "hart" })
    Strip(name + "cast", (c, f) =>
    {
        drawings[name](c);
        using var glow = new SKPaint { Color = SKColor.Parse(f % 2 == 0 ? "#e3bf6b" : "#fff0c3"), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = false };
        c.DrawOval(new SKRect(2, 28 - f, 38, 38), glow);
        Box(c, name == "hart" ? 16 : 15, name == "hart" ? 18 : 10, 4, 3, f % 2 == 0 ? "#ffab54" : "#fff0c3");
        Box(c, 25, name == "hart" ? 18 : 10, 4, 3, "#fff0c3");
    });
foreach (string name in new[] { "wolf", "sentinel" })
    Strip(name + "walk", (c, f) =>
    {
        int stride = new[] { 0, 2, 0, -2 }[f];
        c.Save(); c.ClipRect(new SKRect(0, 0, 40, 31)); c.Translate(0, f % 2 == 1 ? -1 : 0); drawings[name](c); c.Restore();
        Oval(c, 3, 33, 34, 5, "#334d39");
        Box(c, 10, 30 + stride, 6, 7 - stride, "#57685d"); Box(c, 26, 30 - stride, 6, 7 + stride, "#57685d");
    });
Strip("smoke", (c, f) =>
{
    for (int i = 0; i < 3; i++)
    {
        int y = (30 - i * 10 - f * 3 + 40) % 40;
        Oval(c, 15 + (i + f) % 3 - 2, y, 7 + i * 3, 5 + i * 2, i == 0 ? "#609da69a" : "#359da69a");
    }
});
Strip("butterfly", (c, f) =>
{
    int spread = f % 2 == 0 ? 6 : 2;
    Oval(c, 20 - spread, 16, spread, 6, "#dfbb73");
    Oval(c, 21, 16, spread, 6, "#e9d4a0");
    Box(c, 20, 17, 1, 6, "#6c5840");
});
Strip("mote", (c, f) =>
{
    Oval(c, 14, 14, 12, 12, f % 2 == 0 ? "#306ecf94" : "#186ecf94");
    Box(c, 19, 18, 2, 3, f % 2 == 0 ? "#cee7a2" : "#7eaf79");
});
Strip("torch", (c, f) =>
{
    Box(c, 18, 19, 5, 18, "#6d5137"); Box(c, 15, 19, 11, 4, "#96927a");
    Poly(c, "#dc8443", 14,20, 15+f,10, 19,13, 23-f,2+f, 27,14, 25,21);
    Poly(c, "#f5c968", 18,20, 20,12-f, 24,20);
});
foreach (string name in new[] { "nell", "orin" })
    Strip(name, (c, f) =>
    {
        int bob = f == 1 ? -1 : 0;
        Oval(c, 8, 33, 26, 6, "#435137");
        Box(c, 13, 27, 6, 9, "#403d35"); Box(c, 23, 27, 6, 9, "#403d35");
        Box(c, 10, 16+bob, 21, 15, name == "nell" ? "#617c8c" : "#9e6c45");
        Box(c, 15, 19+bob, 12, 12, name == "nell" ? "#d4c99e" : "#52413b");
        Box(c, 14, 5+bob, 14, 13, "#cfa178"); Box(c, 13, 4+bob, 16, 5, name == "nell" ? "#beb6a0" : "#5b4a3c");
        Box(c, 16, 11+bob, 2, 2, "#353830"); Box(c, 24, 11+bob, 2, 2, "#353830");
        Box(c, 7, 19+bob, 5, 9, "#cfa178"); Box(c, 30, 18+bob, 5, 9, "#cfa178");
        if (name == "nell") { Box(c, 5, 14+bob, 3, 15, "#a68a57"); Oval(c, 3, 11+bob, 7, 5, "#dab982"); }
        else { Box(c, 31, 18+bob, 3, 16, "#a68a57"); Box(c, 28, 16+bob, 10, 6, "#9baba3"); }
    });
Asset("floodmark", c => { drawings["ore"](c); Box(c, 9, 19, 22, 2, "#d6cca1"); Box(c, 19, 11, 2, 13, "#e8d9a9"); });
Asset("quarrymarks", c => { drawings["ore"](c); for(int i=0;i<3;i++) Box(c, 12+i*6, 16+i%2*3, 2, 9, "#ffe3a0"); });
Asset("tablet", c => { Box(c, 8, 7, 24, 28, "#626e69"); Box(c, 10, 9, 20, 24, "#939b84"); for(int i=0;i<4;i++) Box(c, 13, 13+i*5, i==3?8:14, 2, "#4b5a52"); });
Strip("float", (c,f) => { Oval(c, 5, 28, 30, 5, "#60888a"); Box(c, 18, 9+f%2, 3, 24, "#c7b985"); Oval(c, 15, 15+f%2, 10, 13, "#b75f43"); Box(c, 17, 16+f%2, 6, 4, "#efda99"); Box(c, 29, 8+f, 2, 5, "#ffebae"); Box(c, 27, 10+f, 6, 1, "#ffebae"); });
Asset("fallenlog", c => { Oval(c, 1, 24, 38, 14, "#455334"); Box(c, 1, 15, 36, 16, "#715137"); Box(c, 2, 16, 34, 3, "#b18b52"); Box(c, 2, 25, 34, 2, "#493b2e"); Oval(c, 29, 15, 10, 16, "#c3a469"); Oval(c, 32, 18, 5, 10, "#886b40"); Box(c, 8, 11, 3, 8, "#577541"); });
foreach (string name in new[] { "spring", "springbloom" })
    Strip(name, (c,f) => { Oval(c, 1, 7, 38, 29, "#75816a"); Oval(c, 4, 10, 32, 22, "#355e63"); Box(c, 9+f, 17, 12, 2, "#80b8b1"); Box(c, 20-f, 25, 9, 1, "#80b8b1"); if(name=="springbloom") { Oval(c, 23, 12, 10, 7, "#66915c"); Box(c, 26, 12, 4, 4, "#f5d9b4"); } });
foreach (string name in new[] { "jetty", "ruinedjetty" })
    Asset(name, c => { drawings["water"](c); Box(c, 4, 2, 4, 35, "#594d38"); Box(c, 31, 2, 4, 35, "#594d38"); for(int y=8;y<35;y+=7) if(name=="jetty"||y%3==0) { Box(c, 3,y,33,5,"#b99b62"); Box(c, 3,y,33,1,"#dbc18a"); } });
foreach (string name in new[] { "returngate", "ruinedgate" })
    Asset(name, c => { Box(c, 4, 9, 7, 28, "#788071"); Box(c, 29, 9, 7, 28, "#788071"); if(name=="returngate") { Box(c, 5, 5, 30, 7, "#aab296"); Box(c, 12, 14, 16, 20, "#56897a"); Poly(c, "#e6d18d", 15,24, 22,16, 22,21, 27,21, 27,26, 22,26, 22,31); } else { Box(c, 9, 30, 13, 6, "#aab296"); Box(c, 22, 34, 7, 4, "#5b6b54"); } });
Asset("bowls", c => { Box(c, 2, 18, 36, 5, "#85623e"); Box(c, 6, 23, 4, 13, "#594834"); Box(c, 30, 23, 4, 13, "#594834"); Oval(c, 5, 10, 13, 10, "#c2ad79"); Oval(c, 22, 10, 13, 10, "#c2ad79"); Oval(c, 7, 11, 9, 5, "#a9613e"); Oval(c, 24, 11, 9, 5, "#a9613e"); });
Asset("wheel", c => { Oval(c, 4, 4, 32, 32, "#564939"); Oval(c, 8, 8, 24, 24, "#b59961"); Oval(c, 11, 11, 18, 18, "#475b39"); Box(c, 17, 7, 5, 26, "#d0b579"); Box(c, 7, 17, 26, 5, "#d0b579"); Oval(c, 15, 15, 10, 10, "#73827c"); });
Asset("lunches", c => { Box(c, 3, 28, 34, 7, "#75583b"); for(int i=0;i<3;i++) { Box(c, 4+i*11, 15+i%2*3, 10, 13, "#c5b68d"); Box(c, 8+i*11, 15+i%2*3, 2, 13, "#795438"); } });
Asset("sign", c => { Box(c, 17, 12, 6, 27, "#755438"); Box(c, 2, 4, 36, 21, "#c4ab72"); Box(c, 5, 7, 30, 15, "#765736"); Box(c, 8, 11, 23, 2, "#ecd89f"); Box(c, 12, 17, 16, 2, "#ecd89f"); });
Asset("banner", c => { Box(c, 7, 1, 4, 38, "#b6a475"); Box(c, 10, 4, 26, 24, "#3d7d77"); Poly(c, "#ecd294", 16,20, 24,8, 31,20); Box(c, 22, 16, 4, 8, "#3d7d77"); });
