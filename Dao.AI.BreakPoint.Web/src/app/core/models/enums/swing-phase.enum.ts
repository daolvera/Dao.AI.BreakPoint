/**
 * Represents the phases of a tennis swing
 */
export enum SwingPhase {
  None = 0,
  Preparation = 1,
  Backswing = 2,
  Contact = 3,
  FollowThrough = 4,
}

/**
 * Display names for swing phases
 */
export const SwingPhaseLabels: Record<SwingPhase, string> = {
  [SwingPhase.None]: 'None',
  [SwingPhase.Preparation]: 'Preparation',
  [SwingPhase.Backswing]: 'Backswing',
  [SwingPhase.Contact]: 'Contact',
  [SwingPhase.FollowThrough]: 'Follow Through',
};
