import { Handedness } from '../enums/handedness.enum';

export interface CompleteProfileRequest {
  name: string;
  ustaRating: number;
  handedness: Handedness;
}
