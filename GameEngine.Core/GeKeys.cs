namespace GameEngine.Core
{
    /*
     * These keys are used to map native key codes to game actions
     * They will be used to create a dictionary that maps keys to actions within the Runners
     * It's done this way to avoid native platform dependencies in the Core project
     *
     * Every runner builds its key map by name, so a value added here reaches the runners
     * as long as the host framework spells it the same way.
    */
    public enum GeKeys
    {
        Q, W, E, R, T, Y, U, I, O, P, A, S, D, F, G, H, J, K, L, Z, X, C, V, B, N, M,
        Space,
        Up, Down, Left, Right
    }
}
