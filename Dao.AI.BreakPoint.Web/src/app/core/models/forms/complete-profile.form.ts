import { FormControl } from '@angular/forms';
import { Handedness } from '../enums/handedness.enum';

export interface CompleteProfileForm {
  /** Player's display name */
  name: FormControl<string>;
  /** USTA rating between 1-7 */
  ustaRating: FormControl<number | null>;
  /** Whether the player is right or left handed */
  handedness: FormControl<Handedness | null>;
}
