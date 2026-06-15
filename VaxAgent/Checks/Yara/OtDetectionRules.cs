using System;
using System.Collections.Generic;

namespace VaxDrive.VaxAgent.Checks.Yara;

/// <summary>
/// A collection of structural detection rules used by the VaxDrive OT agent to identify anomalies
/// or artifacts associated with unauthorized industrial protocols operating outside permitted boundaries.
/// Note: Real-world signatures require extensive testing and tuning in specific environments.
/// </summary>
public static class OtDetectionRules
{
    /// <summary>
    /// Detects the presence of IEC-104 Application Service Data Unit (ASDU) packet structures 
    /// in unexpected processes. This can indicate unauthorized protocol manipulation or variants 
    /// of OT-focused malware (such as Industroyer2).
    /// </summary>
    public static readonly string IEC104_Anomaly_Rule = @"
rule IEC104_ASDU_Anomaly
{
    meta:
        description = ""Detects unexpected IEC-104 ASDU structures in process memory""
        category = ""OT Protocol Violation""
        severity = ""High""
        reference = ""VaxDrive Diagnostics""

    strings:
        // APCI Start Byte (0x68) followed by length, control fields, and Type IDs commonly seen in OT abuse
        // 0x68 [1] [4] 0x2D (Type 45 - Single Command) or 0x2E (Type 46 - Double Command)
        $apci_single_cmd = { 68 ?? ?? ?? ?? ?? 2D }
        $apci_double_cmd = { 68 ?? ?? ?? ?? ?? 2E }

        // M_EI_NA_1 (End of Init - Type 70)
        $apci_end_of_init = { 68 ?? ?? ?? ?? ?? 46 }

    condition:
        any of ($apci_*)
}";

    /// <summary>
    /// Detects artifacts related to the proprietary TriStation protocol used by Schneider Electric 
    /// Triconex Safety Instrumented Systems (SIS). Unauthorized presence of these strings 
    /// may indicate reconnaissance or manipulation attempts targeting SIS (such as Triton variants).
    /// </summary>
    public static readonly string TriStation_Anomaly_Rule = @"
rule TriStation_Protocol_Anomaly
{
    meta:
        description = ""Detects TriStation protocol strings in unauthorized processes""
        category = ""SIS Manipulation""
        severity = ""Critical""
        reference = ""VaxDrive Diagnostics""

    strings:
        // TriStation 1131 program download and logic manipulation strings often seen in diagnostic or attack tooling
        $s1 = ""tsmap.zip"" ascii wide
        $s2 = ""TriStation"" ascii wide nocase
        $s3 = ""inject.bin"" ascii wide
        
        // Protocol-specific opcodes or error codes related to logic updates
        $op_alloc = { 00 00 00 01 00 00 00 00 00 00 00 00 } // Allocate memory command structure

    condition:
        2 of ($s*) or $op_alloc
}";

    /// <summary>
    /// Detects anomalous sequences targeting Siemens S7 logic block manipulation. 
    /// This targets underlying structural manipulation often used by advanced threats (like Stuxnet variants).
    /// Caution: Can cause false positives if run against legitimate Siemens Step7/TIA Portal engineering stations.
    /// </summary>
    public static readonly string SiemensS7_Anomaly_Rule = @"
rule Siemens_S7_Block_Manipulation
{
    meta:
        description = ""Detects anomalous S7 block manipulation sequences""
        category = ""Logic Tampering""
        severity = ""High""
        reference = ""VaxDrive Diagnostics""

    strings:
        // S7comm Download Block command sequence (Job 0x28, Download 0x1B)
        $s7_download = { 32 01 00 00 ?? ?? ?? ?? 00 0e 00 00 04 01 12 0a 10 ?? ?? ?? 00 00 00 ?? ?? ?? }
        
        // Characteristic strings for S7 block injection utilities
        $s1 = ""s7_inject"" ascii wide nocase
        $s2 = ""OB1"" ascii wide fullword
        $s3 = ""OB35"" ascii wide fullword

    condition:
        $s7_download and any of ($s*)
}";
}
