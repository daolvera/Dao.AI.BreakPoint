export enum SwingType {
  ForehandGroundStroke = 'ForehandGroundStroke',
  BackhandGroundStroke = 'BackhandGroundStroke',
  Serve = 'Serve',
  BackhandVolley = 'BackhandVolley',
  ForehandVolley = 'ForehandVolley',
  SmashVolley = 'SmashVolley',
}

export const SwingTypeLabels: Record<SwingType, string> = {
  [SwingType.ForehandGroundStroke]: 'Forehand',
  [SwingType.BackhandGroundStroke]: 'Backhand',
  [SwingType.Serve]: 'Serve',
  [SwingType.BackhandVolley]: 'Backhand Volley',
  [SwingType.ForehandVolley]: 'Forehand Volley',
  [SwingType.SmashVolley]: 'Smash',
};
