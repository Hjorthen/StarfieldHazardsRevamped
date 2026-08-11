ScriptName HaOS_SoakNotificationScript extends ActiveMagicEffect

String Property HazardTypeName Auto

Event OnEffectStart(ObjectReference akTarget, Actor akCaster, MagicEffect akBaseEffect, float afMagnitude, float afDuration)
    Debug.Notification(HazardTypeName + " protection at " + afMagnitude + "%")
EndEvent
