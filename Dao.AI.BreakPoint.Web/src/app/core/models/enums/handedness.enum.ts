export enum Handedness {
  RightHanded = 'RightHanded',
  LeftHanded = 'LeftHanded',
}

export const HandednessLabels: Record<Handedness, string> = {
  [Handedness.RightHanded]: 'Right-Handed',
  [Handedness.LeftHanded]: 'Left-Handed',
};
