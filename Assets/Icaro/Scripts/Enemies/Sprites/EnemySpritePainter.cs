namespace Icaro
{
    // Os inimigos do Bosque desenhados em código ("Ícaro > Sprites > Desenhar inimigos" grava as folhas em
    // Art/Sprites/Inimigos/<Key>): Tatu-lanterna (Cascudo), Flor-canhão (Espinhento), Galhada (Predador),
    // Explorador Tomado (Vagante), Mariposa-lanterna (Zangão) e o Golem; e os chefes: Colosso-cogumelo (Rei Fungo),
    // Oráculo (chefe das Ruínas) e o Sapo Rei (provisório). O Dragão de Magma continua em 3D.
    public static class EnemySpritePainter
    {
        public static EnemyArt[] All() => new EnemyArt[]
        {
            new TatuLanternaArt(),
            new FlorCanhaoArt(),
            new GalhadaArt(),
            new ExploradorArt(),
            new MariposaArt(),
            new GolemArt(),
            new ColossoCogumeloArt(),
            new OraculoArt(),
            new SapoReiArt(),
        };
    }
}
