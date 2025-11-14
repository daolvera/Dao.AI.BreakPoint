export interface UserDto {
  id: number;
  email: string | null;
  displayName: string | null;
  isProfileComplete: boolean;
  playerId: number | null;
}
