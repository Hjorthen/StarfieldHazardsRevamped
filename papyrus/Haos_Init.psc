Scriptname Haos_Init extends Quest 

; Serialized version of last upgrade
Int Property UpgradeVersion = -1 Auto Hidden

Perk Property InitPerk Auto Const Mandatory

; Start Global references
GlobalVariable Property Weather_Mag_NumConcurrentEffects_1 Auto
GlobalVariable Property Weather_Mag_NumConcurrentEffects_2 Auto
GlobalVariable Property Weather_Mag_NumConcurrentEffects_3 Auto
GlobalVariable Property Weather_Mag_NumConcurrentEffects_4 Auto

GlobalVariable Property Weather_Mag_Soak_NumConcurrentEffects_1 Auto
GlobalVariable Property Weather_Mag_Soak_NumConcurrentEffects_2 Auto
GlobalVariable Property Weather_Mag_Soak_NumConcurrentEffects_3 Auto
GlobalVariable Property Weather_Mag_Soak_NumConcurrentEffects_4 Auto

GlobalVariable Property Hazard_Mag_Dmg_Standard Auto
GlobalVariable Property Hazard_Mag_Soak_Standard Auto

GlobalVariable Property AppliedSpell_Dur_Momentary_Soak Auto
GlobalVariable Property AppliedSpell_Mag_Momentary_Soak_RATIO Auto
GlobalVariable Property AppliedSpell_Dur_Momentary Auto
GlobalVariable Property AppliedSpell_Mag_Momentary Auto
GlobalVariable Property AppliedSpell_Dur_Lingering_Soak Auto
GlobalVariable Property AppliedSpell_Dur_Lingering Auto
GlobalVariable Property AppliedSpell_Mag_Lingering Auto
GlobalVariable Property AppliedSpell_Mag_Lingering_Soak_RATIO Auto

GlobalVariable Property EnvironmentalDamage_Mag_NumConcurrentEffects_1 Auto
GlobalVariable Property EnvironmentalDamage_Mag_NumConcurrentEffects_2 Auto
GlobalVariable Property EnvironmentalDamage_Mag_NumConcurrentEffects_3 Auto
GlobalVariable Property EnvironmentalDamage_Mag_NumConcurrentEffects_4 Auto
; End Global references



Event OnQuestInit()
    EnsurePlumbing()
EndEvent

Function EnsurePlumbing()
    EnsurePerk()
    UpgradeIfNecessary()
EndFunction

Function UpgradeIfNecessary()
    ; "latestVersion" is hardcoded and compared to the serialized property "UpgradeVersion".
    ; If they differ, its time to re-apply the changes needed for a mod update..
    Int latestVersion = 1
    If UpgradeVersion < latestVersion
        UpgradeHAOS()
        UpgradeVersion = latestVersion
    EndIf
EndFunction

Function UpgradeHAOS()
    Debug.Notification("Mod HaOS is updating")
    PatchGLOBs()
EndFunction

Function PatchGLOBs()
    Weather_Mag_NumConcurrentEffects_1.SetValue(Haos_GlobalOverrides.Lookup("Weather_Mag_NumConcurrentEffects_1")) 
    Weather_Mag_NumConcurrentEffects_2.SetValue(Haos_GlobalOverrides.Lookup("Weather_Mag_NumConcurrentEffects_2")) 
    Weather_Mag_NumConcurrentEffects_3.SetValue(Haos_GlobalOverrides.Lookup("Weather_Mag_NumConcurrentEffects_3")) 
    Weather_Mag_NumConcurrentEffects_4.SetValue(Haos_GlobalOverrides.Lookup("Weather_Mag_NumConcurrentEffects_4")) 

    Weather_Mag_Soak_NumConcurrentEffects_1.SetValue(Haos_GlobalOverrides.Lookup("Weather_Mag_Soak_NumConcurrentEffects_1")) 
    Weather_Mag_Soak_NumConcurrentEffects_2.SetValue(Haos_GlobalOverrides.Lookup("Weather_Mag_Soak_NumConcurrentEffects_2")) 
    Weather_Mag_Soak_NumConcurrentEffects_3.SetValue(Haos_GlobalOverrides.Lookup("Weather_Mag_Soak_NumConcurrentEffects_3")) 
    Weather_Mag_Soak_NumConcurrentEffects_4.SetValue(Haos_GlobalOverrides.Lookup("Weather_Mag_Soak_NumConcurrentEffects_4")) 

    Hazard_Mag_Dmg_Standard.SetValue(Haos_GlobalOverrides.Lookup("Hazard_Mag_Dmg_Standard")) 
    Hazard_Mag_Soak_Standard.SetValue(Haos_GlobalOverrides.Lookup("Hazard_Mag_Soak_Standard")) 

    AppliedSpell_Dur_Momentary_Soak.SetValue(Haos_GlobalOverrides.Lookup("AppliedSpell_Dur_Momentary_Soak")) 
    AppliedSpell_Mag_Momentary_Soak_RATIO.SetValue(Haos_GlobalOverrides.Lookup("AppliedSpell_Mag_Momentary_Soak_RATIO")) 
    AppliedSpell_Dur_Momentary.SetValue(Haos_GlobalOverrides.Lookup("AppliedSpell_Dur_Momentary")) 
    AppliedSpell_Mag_Momentary.SetValue(Haos_GlobalOverrides.Lookup("AppliedSpell_Mag_Momentary")) 
    AppliedSpell_Dur_Lingering_Soak.SetValue(Haos_GlobalOverrides.Lookup("AppliedSpell_Dur_Lingering_Soak")) 
    AppliedSpell_Dur_Lingering.SetValue(Haos_GlobalOverrides.Lookup("AppliedSpell_Dur_Lingering")) 
    AppliedSpell_Mag_Lingering.SetValue(Haos_GlobalOverrides.Lookup("AppliedSpell_Mag_Lingering")) 
    AppliedSpell_Mag_Lingering_Soak_RATIO.SetValue(Haos_GlobalOverrides.Lookup("AppliedSpell_Mag_Lingering_Soak_RATIO")) 

    EnvironmentalDamage_Mag_NumConcurrentEffects_1.SetValue(Haos_GlobalOverrides.Lookup("EnvironmentalDamage_Mag_NumConcurrentEffects_1")) 
    EnvironmentalDamage_Mag_NumConcurrentEffects_2.SetValue(Haos_GlobalOverrides.Lookup("EnvironmentalDamage_Mag_NumConcurrentEffects_2")) 
    EnvironmentalDamage_Mag_NumConcurrentEffects_3.SetValue(Haos_GlobalOverrides.Lookup("EnvironmentalDamage_Mag_NumConcurrentEffects_3")) 
    EnvironmentalDamage_Mag_NumConcurrentEffects_4.SetValue(Haos_GlobalOverrides.Lookup("EnvironmentalDamage_Mag_NumConcurrentEffects_4")) 
EndFunction


Function EnsurePerk()
    Actor player = Game.GetPlayer()

    if player != None && InitPerk != None && !player.HasPerk(InitPerk)
        player.AddPerk(InitPerk, false)
    endif
EndFunction