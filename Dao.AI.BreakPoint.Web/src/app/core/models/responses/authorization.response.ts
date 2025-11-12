import { UserDto } from '../dtos/user.dto';
import { RefreshTokenResponse } from './refresh-token.response';

export interface AuthorizationResponse extends RefreshTokenResponse {
  user: UserDto;
}
