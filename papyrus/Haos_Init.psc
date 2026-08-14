Scriptname Haos_Init extends Quest 

Perk Property InitPerk Auto Const Mandatory

Event OnQuestInit()
    EnsurePerk()
EndEvent

Function EnsurePerk()
    Actor player = Game.GetPlayer()

    if player != None && InitPerk != None && !player.HasPerk(MyPerk)
        player.AddPerk(InitPerk, false)
    endif
EndFunction