using UnityEditor;
using UnityEngine;
using TMPro;
public static class terminalFontBuilder
{
    [MenuItem("Tools/Signal Terminal/Build dedicated fonts")]
    public static void build()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources/SignalTerminal");
        AssetDatabase.Refresh();
        make("Assets/UI/SignalTerminal/Fonts/Barlow-Regular.ttf","barlowRegular");
        make("Assets/UI/SignalTerminal/Fonts/Barlow-Medium.ttf","barlowMedium");
        make("Assets/TextMesh Pro/Fonts/LiberationSans.ttf","terminalSymbols");
        make("Assets/UI/SignalTerminal/Fonts/NotoMusic-Regular.ttf","terminalMusic");
        var symbols=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/SignalTerminal/terminalSymbols.asset");
        var music=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/SignalTerminal/terminalMusic.asset");
        foreach(string name in new[]{"barlowRegular","barlowMedium"})
        {
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/SignalTerminal/"+name+".asset");
            font.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{music,symbols};
            EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);
        }
        string path="Assets/Resources/SignalTerminal/fontSet.asset";
        var set=AssetDatabase.LoadAssetAtPath<terminalFontSet>(path);
        if(set==null){set=ScriptableObject.CreateInstance<terminalFontSet>();AssetDatabase.CreateAsset(set,path);}
        set.heading=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/ScienceGothic-VariableFont_CTRS,slnt,wdth,wght SDF.asset");
        EditorUtility.SetDirty(set);AssetDatabase.SaveAssetIfDirty(set);
    }
    private static void make(string source,string name)
    {
        string path="Assets/Resources/SignalTerminal/"+name+".asset";
        if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)!=null)return;
        var font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(source),64,7,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
        font.name=name;font.material.name=name+"Material";
        string chars="";for(int i=32;i<256;i++)chars+=(char)i;chars+="–—‘’“”…→←−♯♭";
        font.TryAddCharacters(chars,out string missing);
        font.atlasPopulationMode=AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(var texture in font.atlasTextures){texture.name=name+"Atlas";AssetDatabase.AddObjectToAsset(texture,font);}
        EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);
    }
}
