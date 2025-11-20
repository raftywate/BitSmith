export interface AuthResponse {
    token : string;
    userId : string;
    username : string;
    role : 'User' | 'Admin';
}