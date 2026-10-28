# Coat 1.0.5

## Finitions de la version 1.0.5

Échap revient toujours à Sélection après avoir annulé le geste et retiré la sélection. Les champs du cercle et des escaliers conservent la saisie manuelle ; les boutons **▴ / ▾** et les touches **Haut / Bas** dans le champ ajustent la valeur de 1. Les flèches respectent les limites du nombre de points, du nombre de marches, de la largeur et de l’angle.

L’application courante est dans **app/**, le ZIP dans **dist/**, les guides dans **docs/**, les sources dans **src/**, les anciennes versions dans **archive/** et les résultats de tests dans **artifacts/**. Les fichiers d’exemple séparés ne sont plus livrés. Le plan d’exemple intégré reste accessible depuis Fichier.


**Epicteta Lupercal**

Éditeur Windows de plans et blueprints 2D pour le level design. Interface en anglais par défaut, français disponible dans les préférences, grille à pas fixe, dessin de contours et édition par points, arêtes ou zones fermées.

Le panneau de droite affiche les réglages et les mesures. Le bouton **?** de chaque outil donne accès aux explications au survol ou au clic. La version suit la numérotation des correctifs **1.0.x**.

## Lancer

Ouvrir **app/Coat.exe** depuis les sources, ou **Coat.exe** dans le dossier extrait de l’archive distribuée. Garder les fichiers de ce dossier ensemble : l’exécutable utilise les DLL et les fichiers de configuration voisins. Aucune installation de Coat n’est nécessaire.

Cette version nécessite **Windows 10/11 et .NET Desktop Runtime 8 x64**, déjà présent sur le PC utilisé pour sa création. Elle ne contient pas le runtime .NET. L’application fonctionne hors connexion et n’utilise aucun service externe.

## Exporter en .map — version 1.0.3

1. Dans **Niveaux**, saisir la **Hauteur** et cliquer sur **Appliquer la hauteur**. Les niveaux des anciens plans ont par défaut Z = numéro × 192. Un nouveau niveau voisin se place à 192 unités du niveau existant le plus proche. Les valeurs négatives et décimales sont acceptées.
2. Remplir les zones de murs en blanc (`#FFFFFF`). Dans **Couleur → Export .map**, **Couleur des murs…** ouvre la roue pour choisir une autre couleur. **Définir la couleur actuelle comme mur** utilise la couleur de dessin courante. Ce réglage est propre au projet et ne recolore pas les zones. Les motifs du remplissage ne changent pas la géométrie ; une ligne blanche seule ne devient pas un mur.
   Les zones remplies d’une autre couleur deviennent des sols. Une zone ou une shape sans remplissage ne crée aucun objet.
3. Utiliser **Fichier → Exporter en .map…**. Tous les niveaux sont exportés, même s’ils sont masqués dans la vue 2D. X et Y sont conservés : une unité Coat vaut une unité de la carte.

La base du mur est le Z de son niveau. Son sommet est le Z du prochain niveau existant, dans l’ordre des numéros, même si celui-ci est vide. Au dernier niveau, le sommet est à base +192. Par exemple, un niveau à Z 64 suivi d’un niveau à Z 320 donne des murs hauts de 256 unités. Si le sommet n’est pas au-dessus de la base, l’export indique le niveau à corriger. Ces réglages sont sauvegardés et compatibles avec Annuler/Rétablir.

Les sols sont des dalles de **16 unités d’épaisseur**, de **Z − 16 à Z** : leur surface supérieure correspond au Z du niveau. Les contours orthogonaux produisent des blocs rectangulaires. Seuls les raccords diagonaux et les courbes nécessitent des prismes adaptés. Pour les contours imbriqués, la zone la plus intérieure décide du résultat : couleur des murs → mur, autre couleur → sol, sans remplissage → vide. Les trous restent ouverts et les murs ne fusionnent pas avec les sols.

Le fichier utilise **Valve 220**, avec un en-tête Quake et des brushes sans texture (`__TB_empty`) dans worldspawn. Ouvrir avec **Quake / Valve** dans un éditeur de cartes compatible, puis y affecter les matériaux du jeu. L’export fournit les murs et les sols ; il ne crée pas de plafonds, éclairages ou départs joueur et ne compile pas une carte jouable. Images de référence, annotations et styles de traits restent dans le plan Coat.


**Format du projet :** Coat 1.0.5 conserve le format 6 introduit avec Coat 1.0.2 pour les Z et la couleur des murs. Il lit toujours les formats 1 à 5. Les projets au format 6 nécessitent Coat 1.0.2 ou ultérieur ; l’export des sols nécessite Coat 1.0.3.

## Premier plan

1. Choisir **Dessin** (`D`) et le pas de grille. Cliquer sur une intersection, puis sur la suivante pour tracer un segment. Les clics suivants prolongent le contour.
2. Revenir au premier point pour fermer une zone. `Échap` ou un clic droit sans déplacement termine un tracé ouvert. Les segments déjà créés sont conservés.
3. Choisir **Couleur** (`C`), une couleur et le mode **Remplir une zone**. Cliquer à l’intérieur du contour. Le second mode colore une arête à la fois.
4. Passer à **Sélection** (`V`). Le panneau de droite propose **Points**, **Arêtes** et **Zones**. Glisser la sélection pour la déplacer avec un décalage multiple du pas de grille.
5. En mode **Arêtes**, maintenir `Maj` pour surligner l’arête sous le pointeur, puis la glisser pour créer une extrusion. Le segment original, son décalage et les côtés forment une épaisseur. Le panneau permet aussi de saisir un décalage X/Y exact, puis de cliquer sur **Extruder avec ce décalage**.
6. Enregistrer avec `Ctrl+S`. Le fichier `.coat` contient la géométrie, les couleurs, les niveaux, le niveau actif, le pas de grille, la vue et l’image de référence intégrée.

Un exemple modifiable se trouve dans **Fichier → Charger le plan d’exemple**.

## Niveaux et panneau de droite

Le cadre **Niveaux** reste visible en haut du panneau, même quand les propriétés de l’outil défilent. Les boutons **↓ / ↑** ou les touches **Page ↓ / Page ↑** passent au niveau existant suivant dans la direction choisie. Au bout de la liste, ils créent un nouveau niveau adjacent. Les trous laissés par une suppression ne sont pas recréés lors de la navigation. Le menu déroulant permet de revenir directement à un niveau existant. Les numéros vont de -999 à 999, avec le niveau 0 comme point de départ.

- Le niveau actif est affiché normalement et reçoit le dessin, la sélection, la gomme et le coloriage.
- Les niveaux inférieurs sont atténués progressivement : 60 % pour le niveau juste en dessous, 36 % à deux niveaux, 28 % à trois niveaux, puis une diminution qui reste toujours au-dessus de 12 %. La distance utilise les numéros des niveaux. Ils servent de référence et ne peuvent pas être modifiés tant qu’ils sont inactifs.
- **Affichage → Afficher les niveaux inférieurs** permet de masquer ou réafficher leurs points, lignes, remplissages et annotations. Ils sont visibles par défaut. Le cadrage et les exports respectent ce choix ; le niveau actif reste toujours visible.
- Les niveaux supérieurs sont masqués par défaut. **Affichage → Afficher les niveaux supérieurs** (`H`) révèle leurs contours en rouge très fin, sans remplissage ni annotation. Ils restent non modifiables tant que leur niveau n’est pas actif. Cette option reste active pendant la session jusqu’à sa désactivation.
- L’aperçu en trois quarts représente les plans en gris sur fond noir, avec le plan actif en rouge. Il ne réagit ni à la souris ni au clavier. Au-delà de sept niveaux, il montre les niveaux les plus proches du niveau actif.

Les remplissages du niveau actif sont affichés à 88 % d’opacité. Ceux des niveaux inférieurs conservent une base de 65 %, multipliée par l’opacité de leur niveau.

**Supprimer ce niveau** retire le niveau actif, sa géométrie et ses remplissages. Une confirmation est demandée s’il contient des arêtes. Les autres niveaux conservent leurs numéros ; la vue passe au plus proche. Si le dernier niveau est supprimé, un niveau 0 vide est recréé. La suppression, y compris les groupes concernés, s’annule avec `Ctrl+Z`.

Pour déplacer une sélection vers un autre niveau, saisir son numéro dans **Affecter la sélection au niveau**, puis cliquer sur **Affecter**. La vue passe au niveau de destination. Les couleurs des arêtes et des zones fermées suivent la sélection. Les points sélectionnés emportent leurs arêtes incidentes ; leurs extrémités partagées sont détachées pour conserver les autres arêtes à leur niveau d’origine. L’affectation s’annule avec `Ctrl+Z`.

Les points et intersections de niveaux différents restent indépendants, même aux mêmes coordonnées. Les niveaux sont des plans de travail superposés ; ils ne définissent pas une hauteur physique en unités 3D. L’image de référence reste commune à tous les niveaux.


Coat démarre sans outil actif et sans message au centre de la grille. **Échap** annule le geste en cours, retire la sélection et revient à **Sélection**, même sans sélection. Les éléments déjà créés sont conservés. Cliquer à nouveau sur le bouton de l’outil actif le désactive aussi. Les réglages **Image de référence** apparaissent sans outil actif. Les explications sont accessibles via le bouton **?**.

## Shapes : rectangles et cercles

Choisir **Shapes** (`R`), puis le type de forme dans le panneau de droite.

- **Rectangle** : placer deux coins opposés par deux clics, ou cliquer-glisser. Le contour initial comporte quatre points, aimantés à la grille.
- **Cercle** : placer le centre puis un point pour le rayon, par deux clics ou cliquer-glisser. Le champ **Points** règle la précision de 3 à 128 sommets, avec 32 par défaut. Le centre et le rayon suivent le pas de grille ; les sommets intermédiaires restent sur un polygone régulier, sans déformation par aimantation individuelle.

Un aperçu accompagne le tracé. `Échap` ou clic droit annule une forme en cours. Chaque forme complète compte pour une seule opération d’annulation. Ses points et arêtes sont ensuite éditables, et son intérieur peut être rempli. Les intersections avec une géométrie existante du même niveau peuvent ajouter des points au contour.

## Groupes et déplacements

Sélectionner des points, arêtes ou zones puis utiliser **Ctrl+G** (ou **Édition → Grouper la sélection**). Cliquer sur un membre du groupe sélectionne l’ensemble ; `Ctrl` + clic permet d’ajouter ou retirer le groupe de la sélection. **Ctrl+Maj+G** dissocie les groupes sélectionnés sans supprimer leur géométrie.

Les groupes sont sauvegardés dans le projet, suivent les déplacements et affectations de niveau, et sont conservés lors du découpage d’arêtes par des intersections. Une duplication crée un groupe indépendant. Regrouper une sélection contenant des groupes les réunit en un seul groupe, sans groupes imbriqués. Les arêtes continuent de partager leurs sommets avec les contours voisins : un groupe n’isole pas la topologie.

Les champs **Décalage X / Décalage Y** conservent exactement le texte saisi après **Déplacer la sélection**, ainsi qu’en changeant d’outil ou de sélection pendant la session. On peut ainsi appliquer le même décalage plusieurs fois. Ils reviennent à zéro au prochain lancement de Coat.


## Navigation et raccourcis

| Action | Commande |
|---|---|
| Dessin / sélection / gomme / couleur | `D` / `V` / `E` / `C` |
| Rectangles et cercles (Shapes) | `R` |
| Points / arêtes / zones | `Z` / `X` / `F` |
| Pas de grille 1, 2, 4, 8, 16, 32, 64, 128, 256, 512 | Touches `1` à `0`, rangée supérieure ou pavé numérique |
| Déplacer la vue | Clic milieu ou droit maintenu ; `Espace` + clic gauche ; flèches |
| Déplacer la vue plus vite | `Maj` + flèches |
| Zoom autour du pointeur | Molette |
| Cadrer tout le plan | `A` / bouton **Cadrer** |
| Sélection multiple | `Ctrl` + clic |
| Sélection rectangulaire | Glisser dans le vide ; éléments entièrement inclus |
| Extrusion d’arêtes | `Maj` + glisser |
| Annuler un glissement / arrêter un tracé | `Échap` |
| Revenir à Sélection et annuler le geste | `Échap` |
| Niveau supérieur / inférieur | `Page ↑` / `Page ↓` |
| Tout sélectionner / dupliquer / supprimer | `Ctrl+A` / `Ctrl+D` / `Suppr` |
| Grouper / dissocier | `Ctrl+G` / `Ctrl+Maj+G` |
| Annuler / rétablir | `Ctrl+Z` / `Ctrl+Y` ou `Ctrl+Maj+Z` |
| Nouveau / ouvrir / enregistrer | `Ctrl+N` / `Ctrl+O` / `Ctrl+S` |
| Enregistrer sous | `Ctrl+Maj+S` |
| Importer une image | `Ctrl+I` |
| Grille / longueurs | `G` / `M` |
| Aide | `F1` |

Quand un champ de texte est actif, le clavier sert à sa saisie. Cliquer sur la grille pour retrouver les raccourcis d’édition.

## Images et exports

Importer un PNG, JPG ou BMP depuis le panneau de droite sans outil actif, ou avec `Ctrl+I`. Régler l’opacité, les coordonnées du coin supérieur gauche et la largeur en unités ; les proportions sont conservées. La grille et le dessin restent au-dessus de l’image. Limite d’import : 25 Mo, 64 mégapixels.

**Fichier → Exporter en PNG** enregistre uniquement la grille et le plan, avec les règles de coordonnées et la barre d’échelle. Les menus, les outils, le panneau latéral et le badge de vue sont exclus. Le cadrage et le zoom affichés sont conservés, à une résolution doublée. Pour voir tout le dessin avant l’export, utiliser **Cadrer**. La visibilité de la grille et des longueurs suit l’affichage courant ; les surbrillances de sélection et gestes en cours ne sont pas imprimés. **Exporter en SVG** produit un plan vectoriel sans grille, avec ses remplissages et l’image de référence intégrée.

Les deux exports suivent l’affichage : niveaux inférieurs atténués ou masqués, niveaux supérieurs masqués ou représentés par leurs contours rouges selon les options Affichage. Le PNG conserve uniquement la grille, le plan et l’échelle, sans les menus.

## Comportement de cette version

- L’unité est abstraite : 1 unité Coat peut représenter l’unité choisie pour votre moteur de jeu.
- Les nouveaux points se placent sur la grille. Les croisements de lignes d’un même niveau créent automatiquement un sommet commun, éventuellement entre deux intersections de grille.
- Le zoom change uniquement l’affichage. À faible zoom, certaines lignes de grille sont masquées pour garder la vue lisible ; le pas d’aimantation est conservé et indiqué.
- Une « zone » est un contour fermé en 2D. Les contours imbriqués sont des zones superposées ; ils ne définissent pas de trous booléens.
- Déplacer un point ou une arête déforme aussi les arêtes qui partagent ses points. L’extrusion suit le décalage X/Y de la souris ; elle n’impose pas une direction perpendiculaire.
- La gomme efface une arête au clic, ou le remplissage d’une zone. Supprimer une zone sélectionnée supprime ses arêtes de contour, y compris celles partagées avec une autre zone.
- L’historique conserve jusqu’à 100 modifications. Les déplacements de vue et les choix d’outil n’ajoutent pas d’étapes à cet historique.
- Les sauvegardes utilisent un fichier temporaire puis un remplacement du fichier de destination. Les modifications non enregistrées déclenchent une demande avant fermeture ou remplacement du plan. Pas de sauvegarde automatique dans cette version.
- L’export `.map` crée les murs et les sols selon les remplissages ; il n’ajoute pas automatiquement de plafonds ou d’entités de jeu.

## Sources et compilation

Les sources C# / WPF se trouvent dans `src`. Le projet ne dépend d’aucun paquet tiers. Le SDK .NET 8 et les packs de ciblage Windows Desktop doivent être installés.

Depuis PowerShell :

```powershell
.\build.ps1
```

Pour les tests de géométrie, de sauvegarde et de rendu de l’interface :

```powershell
.\test.ps1
```

**./build.ps1 -Package** produit le ZIP de livraison dans **dist/**. Les anciens binaires et ZIP sont regroupés dans **archive/**.

Les résultats et les captures de rendu sont écrits dans `artifacts/tests`. Le plan `.coat` est du JSON versionné et lisible.


## Nouveautés 0.4 : raccourcis, annotations et styles

**Ctrl+,** ou **Édition → Préférences** ouvre les raccourcis, classés par catégories et filtrables par nom ou touche. **Ergonomique** propose Z/X/F pour les modes de sélection, A pour cadrer, W pour déplacer, Maj+R pour tourner, Maj+S pour redimensionner et Maj+H pour les niveaux inférieurs. **Classique** retrouve P/L/F et Origine. Ces préréglages modifient le brouillon ; Enregistrer les applique, Annuler les abandonne. Les raccourcis personnalisés restent prioritaires lors de la migration. Cliquez sur une touche puis pressez la combinaison souhaitée. Les doublons sont refusés ; pour échanger deux touches, passez temporairement par une touche libre. Enregistrer applique les réglages et les conserve dans `%LOCALAPPDATA%/Coat/preferences.json`. Annuler ne change rien ; Par défaut restaure la configuration initiale dans la fenêtre, à enregistrer pour la valider. Les gestes Maj+glisser pour extruder, Ctrl+clic et Espace+glisser restent fixes. Les touches citées dans ce guide sont les valeurs par défaut.

**Texte (T)** : cliquez sur la grille pour placer une annotation. Cliquez sur un texte existant pour le sélectionner. Le panneau de droite permet de modifier le texte (plusieurs lignes), la taille en unités, la couleur et les coordonnées ; validez avec **Appliquer le texte**. Glissez le texte pour le déplacer sur la grille. Suppr le supprime, Ctrl+D le duplique. Une annotation appartient à un niveau, peut être affectée à un autre niveau, et suit les mêmes règles de visibilité. Les opérations appliquées sont annulables avec Ctrl+Z.

**Sélection → Arêtes** : choisissez Continu, Long ou Court dans Style des lignes pour appliquer un trait continu ou des tirets longs/courts. Avec une zone sélectionnée, le style s’applique à son contour.

**Sélection → Zones** : choisissez Uni, Bandes ou Points dans Motif du remplissage. Les bandes sont verticales ; les motifs sont plus foncés que la couleur de base. L’outil Couleur permet également de choisir un motif ou un style avant de peindre.

Les annotations et styles sont conservés dans les fichiers .coat et inclus dans les exports PNG et SVG. Les anciens projets s’ouvrent dans Coat 0.4 ; un fichier enregistré au nouveau format 4 nécessite Coat 0.4 ou une version ultérieure.


## Nouveautés 0.5 : roue de couleurs et export de la vue

Dans l’outil **Couleur**, le bouton **Roue de couleurs…** ouvre une palette avec un disque de teinte/saturation, un réglage de luminosité, une prévisualisation avant/après et un champ hexadécimal. Cliquer ou glisser dans le disque pour choisir une couleur ; les curseurs permettent de préciser le résultat, y compris le blanc, le gris ou le noir. **Utiliser cette couleur** valide le choix pour les prochains coloriages ; **Annuler** laisse la couleur active inchangée. Le même bouton est disponible dans les annotations ; valider ensuite **Appliquer le texte**.

L’export PNG garde désormais la vue affichée et son échelle. Depuis la correction 0.5.1, seule la surface de dessin est exportée, sans les menus ni le panneau latéral. Le SVG reste un export vectoriel du plan. Coat 0.5 utilisait le format 4. Coat 0.6 lit toujours ce format et enregistre désormais au format 5.


## Nouveautés 0.6 : presse-papiers, déplacement par surface et niveaux supérieurs

- **Ctrl+C / Ctrl+X / Ctrl+V** : copier, couper et coller la sélection. Ces actions figurent aussi dans **Édition**, et leurs raccourcis sont modifiables dans les préférences. Les champs de texte conservent leurs raccourcis habituels pour éditer du texte.
- Le presse-papiers Windows permet de copier entre projets et fenêtres Coat. Géométrie, groupes entièrement inclus, couleurs, motifs, styles de lignes et annotations sont conservés. Un point seul peut aussi être copié et déplacé indépendamment.
- Le collage se place près du centre de la vue, sur le niveau actif, en respectant le pas de grille. Des collages successifs se décalent d’un pas vers la droite et vers le bas. La copie est sélectionnée pour être déplacée. Comme le dessin habituel, les géométries qui se superposent partagent leurs points et sont découpées aux intersections.
- Couper supprime uniquement après une copie réussie. Couper et coller sont annulables avec **Ctrl+Z**.
- En sélection **Points** ou **Arêtes**, une forme fermée entièrement sélectionnée peut se déplacer en glissant son intérieur, même sans remplissage. Le déplacement par point ou arête reste disponible. Une sélection partielle se déplace par ses points/arêtes ; **Maj+glisser sur une arête** reste l’extrusion.
- **H** ou **Affichage → Afficher les niveaux supérieurs** montre/cache leurs traits rouges très fins. Il s’agit de repères non sélectionnables ; les couleurs et textes supérieurs restent masqués. L’option est désactivée au démarrage et suit les exports PNG/SVG. Les raccourcis personnalisés existants sont conservés ; si un nouveau raccourci est déjà occupé, une combinaison libre est attribuée dans Préférences.

Le format 5 conserve les points indépendants collés. Les formats 1 à 4 restent lisibles ; pour ouvrir un projet enregistré par Coat 0.6, utiliser Coat 0.6 ou ultérieur.


## Nouveautés 0.7 : transformations et escaliers

### Transformations dans Sélection

Le panneau droit propose **Translation** (flèches), **Rotation** (cercles) et **Échelle** (petit cube vers grand cube). Choisir un mode, puis glisser un point, une arête ou l’intérieur d’une forme entièrement sélectionnée.

- **Translation** : déplacement sur la grille, ou valeurs Décalage X/Y avec le bouton Déplacer la sélection. Les valeurs saisies sont conservées. L’extrusion Maj+glisser sur une arête s’utilise dans ce mode.
- **Rotation** : pivot au centre de la boîte englobante de la sélection. Glisser autour du pivot ; si le geste commence au centre, glisser horizontalement. La souris règle l’angle par pas de 1°, ou 15° avec Maj. Le champ Angle permet une valeur précise ; un angle positif tourne dans le sens antihoraire.
- **Échelle** : à la souris, agrandissement/réduction uniforme autour du même centre. Le champ Facteur accepte 2 pour doubler, 0,5 pour réduire de moitié. Décocher Échelle uniforme pour saisir des facteurs X/Y indépendants. Un facteur négatif crée un miroir ; zéro est interdit. Les valeurs absolues numériques vont de 0,001 à 1000. Le geste souris va de 0,01 à 1000 ; Maj règle le facteur par pas de 0,1.

Rotation et échelle conservent la forme sans arrondir individuellement les sommets à la grille. Elles s’appliquent à la géométrie ; les annotations gardent leur déplacement et leurs réglages de texte dédiés. Les couleurs, motifs et groupes sont conservés. Échap annule un geste en cours ; Ctrl+Z annule la transformation complète. Comme pour le dessin, les intersections avec la géométrie existante sont normalisées.

### Shapes → Escaliers

Choisir **Escaliers**, puis **Droits**, **En L**, **En U** ou **En colimaçon**. Deux clics ou un cliquer-glisser créent la forme avec son aperçu.

- **Droits, L et U** : le premier clic place le milieu de l’entrée. La direction dominante de la souris choisit l’axe horizontal/vertical et le sens. La distance projetée sur cet axe règle la longueur cumulée des volées, hors palier. Largeur est saisie en unités. Les variantes L/U disposent d’un palier et d’un choix de virage gauche/droite ; le U possède un espace entre ses deux volées.
- **Colimaçon** : premier clic au centre, puis la souris règle le rayon extérieur et l’orientation du premier rayon. Angle définit l’ouverture de 15° à 360°. Le sens de rotation est réglable à gauche ou à droite. Le vide central représente 22 % du rayon extérieur.
- **Marches** : 2 à 128 marches au total. En L/U, elles sont réparties entre les deux volées ; les paliers sont en plus. Le départ et la longueur ou le rayon suivent la grille. Les divisions sont espacées régulièrement, même si elles sont plus fines que le pas de grille.

Chaque escalier est un groupe de points et d’arêtes, comprenant les contours et les traits de marches. Il se déplace, tourne, se redimensionne et se copie comme les autres groupes ; dissocier avec Ctrl+Maj+G permet une édition séparée. Les paramètres du panneau règlent les prochains escaliers : les formes déjà placées restent de la géométrie modifiable. La création complète s’annule en une seule étape.



## Nouveautés 0.8 : palette personnelle, pipette et marches

Les 12 cases de l’outil **Couleur** sont vides au premier lancement. Pour enregistrer une couleur, ouvrir **Roue de couleurs…**, la choisir, puis cliquer sur le carré **+**. La couleur rejoint la première case libre ; une couleur déjà enregistrée ne prend pas une deuxième place. Si les 12 cases sont occupées, un message invite à libérer une case.

Cliquer sur une case remplie choisit sa couleur. **Clic droit → Supprimer** libère uniquement cette case, sans décaler les autres couleurs et sans changer la couleur active ni le plan. Les couleurs enregistrées sont partagées entre le panneau droit et la roue. Les palettes sont conservées entre les lancements dans `%LOCALAPPDATA%/Coat/palette.json`, indépendamment des raccourcis et des fichiers `.coat`. Le bouton + enregistre immédiatement : annuler le choix de couleur ensuite n’annule pas cet enregistrement explicite.

Dans la roue, **Pipette…** masque temporairement la fenêtre de couleur et permet de prélever un pixel du plan affiché, y compris une image de référence ou un remplissage. Le curseur montre un aperçu et le code hexadécimal. Cliquer choisit la couleur et revient à la roue ; **Échap** ou **clic droit** annule. Cliquer ensuite sur **+** pour la mémoriser, ou **Utiliser cette couleur** pour l’appliquer à l’outil. Le prélèvement porte sur l’apparence visible du plan, avec son opacité, ses motifs et sa grille.

Les nouveaux **escaliers** ont des **contours continus** et des **marches en tirets longs**, y compris en L, en U et en colimaçon. L’aperçu montre ces styles avant placement. Il s’agit du style natif **Long**, conservé après transformation, copie, sauvegarde et export, et modifiable dans Sélection → Style des lignes. Les projets existants conservent leurs styles.


## Nouveautés 1.0 : English / Français

L’anglais est la langue par défaut, y compris après une mise à niveau depuis une version sans préférence de langue. Ouvrir **Edit → Preferences → Interface language**, choisir **Français**, puis **Save** pour passer au français. Dans l’interface française : **Édition → Préférences → Langue de l’interface**. Le changement s’applique dès l’enregistrement, sans redémarrage, et reste mémorisé entre les lancements. Annuler ne change rien.

Les menus, panneaux d’outils, raccourcis, messages, confirmations, aide et indications de la grille sont traduits. La terminologie anglaise utilise **Vertices / Edges / Faces**, **Move / Rotate / Scale**, **Fill**, **Long dashes**, **Eyedropper** et **Straight / L-shaped / U-shaped / Spiral stairs**. Les fenêtres système d’ouverture et d’enregistrement de fichiers suivent la langue de Windows.

La langue et les raccourcis sont enregistrés ensemble dans `%LOCALAPPDATA%/Coat/preferences.json`. Les anciens raccourcis personnalisés sont conservés. Le changement de langue préserve le plan ouvert, la sélection, la vue, l’historique et la palette. Les annotations et noms de groupes saisis dans les projets ne sont pas traduits. Le format `.coat` est désormais le format 6.

Le guide **README-English.md** (livré sous le nom **README.md** dans l’archive) présente toutes les fonctions en anglais.
