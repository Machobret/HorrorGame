# Flare firing last-round regression

PlayerStateAttack.UpdateRateAttack previously rechecked ammo after consuming the current round. A one-round flare gun immediately failed that recheck and called AttackMontage.Interrupt(), stopping the coroutine scheduled by AttackMontage.Play before FlareGunAttack.StartAttack could run. Direct StartAttack tests bypassed this failure.

The attack state now stops repetition when the rate is zero, the burst count is exhausted, or ammunition runs out, while allowing the pending shot to finish. This also preserves the last shot of other rate-controlled weapons.

Validated in a full isolated Unity 6000.3.11f1 project using synthetic right/left mouse events through PlayerInputListener and the real player state machine. Before: loaded flare consumed, zero new projectiles. After: loaded flare consumed, exactly one new projectile. Scene startup, equipment, spare ammo, demo flight/trail/light, landing and camera captures passed as well.

Logs: C:/Users/Mangis/AppData/Local/Temp/flare-input-before-fix2.log and flare-input-after-fix.log. Unity's unrelated editor search-index startup exception is excluded only by its editor-specific stack trace.
