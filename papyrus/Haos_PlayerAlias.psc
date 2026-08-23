Scriptname Haos_PlayerAlias extends ReferenceAlias

Event OnPlayerLoadGame()
    (GetOwningQuest() as Haos_Init).EnsurePlumbing()
EndEvent