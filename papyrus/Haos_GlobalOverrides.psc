ScriptName Haos_GlobalOverrides


Float Function Lookup(String keyVal) Global
If keyVal == "Weather_Mag_NumConcurrentEffects_1"
	Return 0.75
ElseIf keyVal == "Weather_Mag_NumConcurrentEffects_2"
	Return 0.45000002
ElseIf keyVal == "Weather_Mag_NumConcurrentEffects_3"
	Return 0.36
ElseIf keyVal == "Weather_Mag_NumConcurrentEffects_4"
	Return 0.32400003
ElseIf keyVal == "Weather_Mag_Soak_NumConcurrentEffects_1"
	Return 3.5
ElseIf keyVal == "Weather_Mag_Soak_NumConcurrentEffects_2"
	Return 2.1000001
ElseIf keyVal == "Weather_Mag_Soak_NumConcurrentEffects_3"
	Return 1.6800001
ElseIf keyVal == "Weather_Mag_Soak_NumConcurrentEffects_4"
	Return 1.5120001
ElseIf keyVal == "Hazard_Mag_Dmg_Standard"
	Return 4
ElseIf keyVal == "Hazard_Mag_Soak_Standard"
	Return 1.5
ElseIf keyVal == "AppliedSpell_Dur_Momentary_Soak"
	Return 0
ElseIf keyVal == "AppliedSpell_Mag_Momentary_Soak_RATIO"
	Return 2
ElseIf keyVal == "AppliedSpell_Dur_Momentary"
	Return 1
ElseIf keyVal == "AppliedSpell_Mag_Momentary"
	Return 10
ElseIf keyVal == "AppliedSpell_Dur_Lingering_Soak"
	Return 3
ElseIf keyVal == "AppliedSpell_Dur_Lingering"
	Return 5
ElseIf keyVal == "AppliedSpell_Mag_Lingering"
	Return 20
ElseIf keyVal == "AppliedSpell_Mag_Lingering_Soak_RATIO"
	Return 1
ElseIf keyVal == "EnvironmentalDamage_Mag_NumConcurrentEffects_1"
	Return 0.9524
ElseIf keyVal == "EnvironmentalDamage_Mag_NumConcurrentEffects_2"
	Return 0.57144004
ElseIf keyVal == "EnvironmentalDamage_Mag_NumConcurrentEffects_3"
	Return 0.45715204
ElseIf keyVal == "EnvironmentalDamage_Mag_NumConcurrentEffects_4"
	Return 0.41143686
EndIf
EndFunction
