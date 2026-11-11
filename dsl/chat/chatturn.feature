# Chat turn exit protocol (step 7.3): ONE ordered list for every way out of a turn. Today: 8 exits, 32 call sites in 5 files (MCPChatWindow, .Drain, .EventHandlers, .Selector, .Session).
# Effects are ports (MCPChatWindow.TurnPorts.cs) and their bodies stay native; so does the detection of an exit (a TurnDone event, the inactivity timer, a click). Rules: chatturn.examples.
FEATURE chatTurn USES elm
  STATE phase : enum idle|sending|receiving = idle
  STATE exit : enum none|done|error|dead|timeout|cancel|abort|reset|disable = none
  STATE asks : int 0..8 = 0
  PORT lockReload()
  PORT undoStart()
  PORT unlockReload()
  PORT cancelAsks()
  PORT undoClose()
  PORT syncAssets()
  PORT refreshResolver()
  PORT noteProcessExited()
  PORT noteTimedOut()
  PORT finalizeAssistant()
  PORT errorChip()
  PORT afterAnswer()
  PORT restoreButton()
  PORT offerApprove()
  PORT restartBackend()
  PORT resetTurn()
  DECISION canSend OF phase
  DECISION active OF phase
  ACTION send WHEN canSend -> phase = sending; exit = none; CALL lockReload; CALL undoStart
  ACTION firstToken WHEN active -> phase = receiving
  ACTION askRaised WHEN active -> asks += 1
  ACTION askAnswered WHEN active -> asks -= 1
  ACTION turnDone WHEN active -> phase = idle; asks = 0; exit = done
  ACTION fail WHEN active -> phase = idle; asks = 0; exit = error
  ACTION deadProcess WHEN active -> phase = idle; asks = 0; exit = dead
  ACTION timeout WHEN active -> phase = idle; asks = 0; exit = timeout
  ACTION cancel WHEN active -> phase = idle; asks = 0; exit = cancel
  ACTION abort WHEN active -> phase = idle; asks = 0; exit = abort
  ACTION resetSession WHEN active -> phase = idle; asks = 0; exit = reset
  ACTION disable WHEN active -> phase = idle; asks = 0; exit = disable
  # THE exit protocol: declaration order is effect order, the same for all eight exits.
  # 1 resources first: a failing UI step cannot keep the lock, the undo group or an ask
  ON phase BECOMES idle -> CALL unlockReload
  ON phase BECOMES idle -> CALL cancelAsks
  ON phase BECOMES idle -> CALL undoClose
  # 2 the transcript: sync after the unlock (D2.3), resolver before finalize (F20), the note extends the open bubble, finalize before the error chip (T1.2)
  ON exit BECOMES done -> CALL syncAssets
  ON exit BECOMES done -> CALL refreshResolver
  ON exit BECOMES dead -> CALL noteProcessExited
  ON exit BECOMES timeout -> CALL noteTimedOut
  ON phase BECOMES idle -> CALL finalizeAssistant
  ON exit BECOMES error -> CALL errorChip
  ON exit BECOMES done -> CALL afterAnswer
  ON exit BECOMES done -> CALL restoreButton
  ON exit BECOMES error -> CALL restoreButton
  ON exit BECOMES done -> CALL offerApprove
  # 3 the backend, then the per-turn scratch last: the timeout note reads it
  ON exit BECOMES cancel -> CALL restartBackend
  ON phase BECOMES idle -> CALL resetTurn
  SHOW phase, asks
  EXAMPLES chatturn.examples
END
