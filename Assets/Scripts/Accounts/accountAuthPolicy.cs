using System;
using System.Net.Mail;

// Public settings verified against this project's Firebase Auth policy on 2026-11-07.
// Keep these in sync when changing Auth policy; the server remains authoritative.
public static class accountAuthPolicy
{
    public const int minimumPasswordLength = 6;
    public const int resendCooldownSeconds = 60;
    public const int maximumPasswordLength = 4096;
    public const string passwordRequirements = "Use at least 6 characters. A long, unique password is recommended.";
    public static bool validEmail(string value)
    {
        value=(value??"").Trim();
        if(value.Length==0 || value.Length>254 || value.IndexOfAny(new[]{' ','\t','\r','\n','<','>'})>=0)return false;
        try { var address=new MailAddress(value);return address.Address==value && address.Host.Contains(".") && !address.Host.EndsWith("."); }
        catch(FormatException){return false;}
    }
    public static string passwordError(string value)
    {return string.IsNullOrEmpty(value)?"Enter a password":value.Length<minimumPasswordLength?"Use at least 6 characters":value.Length>maximumPasswordLength?"Use no more than 4096 characters":"";}
}
