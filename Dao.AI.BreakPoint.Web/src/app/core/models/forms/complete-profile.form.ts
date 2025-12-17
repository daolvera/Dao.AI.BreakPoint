import { FormControl } from '@angular/forms';

export interface CompleteProfileForm {
  /** Player's display name */
  name: FormControl<string>;
  /** USTA rating between 1-7 */
  ustaRating: FormControl<number | null>;
}
