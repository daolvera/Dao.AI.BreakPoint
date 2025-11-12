import { OAuthProvider } from '../enums/oatuh-provider.enum';

export interface UserDto {
  id: number;
  email: string;
  name: string;
  isProfileComplete: boolean;
  playerId: number | null;
  externalProvider: OAuthProvider;
}
