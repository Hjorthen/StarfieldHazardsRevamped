ScriptName Haos_SoakDepletionBeepEffect extends ActiveMagicEffect

ActorValue[] Property SoakValues Auto Const
ActorValue Property SetValue Auto Const

Float Property SoakLow = 0.0 Auto Const
Float Property SoakHigh = 40.0 Auto Const
Float Property TargetMin = 0.0 Auto Const
Float Property TargetMax = 20.0 Auto Const

Event OnEffectStart(ObjectReference akTarget, Actor akCaster, MagicEffect akBaseEffect, float afMagnitude, float afDuration)
    StartTimer(1)
EndEvent

Event OnEffectFinish(ObjectReference akTarget, Actor akCaster, MagicEffect akBaseEffect, float afMagnitude, float afDuration)
        ; Restore soak value back to full
        akTarget.RestoreValue(SetValue, 1000)
EndEvent
    
Event OnTimer(int aiTimerID)
    Actor player = Game.GetPlayer()
    UpdateSoak(player)
    StartTimer(1)
EndEvent

Function UpdateSoak(Actor akTarget)
    Float minSoak = GetMinValue(akTarget)

    Float lerped = Map(minSoak, SoakLow, SoakHigh, TargetMin, TargetMax)
    Float currentValue = akTarget.GetValue(SetValue)

    Float diff = lerped - currentValue
    If diff < 0
        akTarget.DamageValue(SetValue, diff * -1)
    Else
        akTarget.RestoreValue(SetValue, diff)
    EndIf

EndFunction

Float Function Map(Float value, Float aLow, Float aHigh, Float bLow, Float bHigh)
    Return bLow + ((value - aLow)*(bHigh - bLow)) / (aHigh - aLow)
EndFunction

Float Function GetMinValue(Actor akTarget)
    Float lowest = akTarget.GetValue(SoakValues[0])
    Int i = 1
    While i < SoakValues.Length
        Float val = akTarget.GetValue(SoakValues[i])
        If val < lowest
            lowest = val
        EndIf
        i += 1
    EndWhile
    Return lowest
EndFunction