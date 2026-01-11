export enum SwingType {
  ForehandGroundStroke,
  BackhandGroundStroke,
}

/**
 * Display names for swing types
 */
export const SwingTypeLabels: Record<SwingType, string> = {
  [SwingType.ForehandGroundStroke]: 'Forehand Ground Stroke',
  [SwingType.BackhandGroundStroke]: 'Backhand Ground Stroke',
};
