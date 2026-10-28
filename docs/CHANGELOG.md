# Coat 1.0.5 — Epicteta Lupercal

- Échap annule le geste, retire la sélection et revient à Sélection, y compris sans sélection.
- Préférences par catégories, avec recherche et préréglages Ergonomique / Classique. La capture d’un raccourci nécessite un clic explicite.
- Raccourcis ergonomiques : Z/X/F pour la sélection, A pour cadrer, W pour déplacer, Maj+R pour tourner, Maj+S pour redimensionner, Maj+H pour les niveaux inférieurs et Ctrl+, pour les préférences.
- Migration des anciennes valeurs par défaut, en conservant les raccourcis personnalisés et en évitant les conflits.
- Cercles et escaliers : saisie manuelle, petites flèches et touches Haut/Bas pour ajuster les valeurs, avec respect des limites.
- Livraison sans fichiers d’exemple, application dans app/, ZIP dans dist/, guides dans docs/, anciennes versions dans archive/ et résultats des tests dans artifacts/.
- Format .coat 6 et export .map inchangés.
- Vérification de publication : messages de compilation et de tests en anglais, liens du README adaptés à GitHub et prérequis Windows explicites.
- Mesures de l’aperçu selon la langue de l’interface et titre SVG en anglais. Paramètres régionaux et noms Unicode vérifiés par les tests.

# Coat 1.0.4 — Epicteta Lupercal

- Numérotation corrigée : les correctifs précédents sont 1.0.1, 1.0.2 et 1.0.3.
- Panneau de droite allégé : réglages, actions et mesures restent visibles. Les explications sont regroupées dans le bouton ? de chaque outil, au survol ou au clic.
- Suppression des titres redondants et des paragraphes permanents dans les panneaux des outils et des niveaux.
- Elevation Z devient Height en anglais, Hauteur en français. Calcul des altitudes et exports inchangés.

# Coat 1.0.3 — Epicteta Lupercal

- Export .map : la couleur des murs crée les murs, les autres remplissages créent des sols et les zones sans remplissage ne créent aucun objet.
- Sols de 16 unités d’épaisseur, surface au Z du niveau. Les zones vides imbriquées restent des trous ; murs et sols restent séparés.
- Libellés génériques « Exporter en .map » et « Export .map » dans les menus, panneaux et guides. Exemple renommé Example-Map.
- Contours orthogonaux décomposés en rectangles, diagonales et courbes conservées. Format de projet 6 inchangé.

# Coat 1.0.2 — 23 octobre 2026 — Epicteta Lupercal

- Export .map Valve 220 au format .map depuis le menu Fichier : les zones remplies en blanc deviennent des murs. Couleur configurable dans l’outil Couleur.
- Hauteur Z réglable dans le cadre Niveaux. Base du mur au Z du niveau, sommet au Z du niveau supérieur existant, ou +192 unités au dernier niveau.
- Décomposition des contours orthogonaux en brushes rectangulaires, conservation des diagonales, courbes et ouvertures. Tous les niveaux sont exportés indépendamment de leur visibilité.
- Z et couleur des murs sauvegardés dans le format .coat 6, avec Annuler/Rétablir. Lecture des formats 1 à 5 conservée.
- Exemple .coat et .map fourni. L’export porte sur la géométrie des murs ; les matériaux et autres éléments du jeu se règlent dans un éditeur de cartes.

# Coat 1.0.1 — 22 octobre 2026 — Epicteta Lupercal

- Correction des ralentissements lors de l’ajout de géométrie : index locaux des points et des arêtes, rejet des segments éloignés avant calcul des intersections, recherches limitées au niveau concerné.
- Affichage → Afficher les niveaux inférieurs : masque ou réaffiche les points, arêtes, remplissages et annotations des niveaux inférieurs, indépendamment des contours des niveaux supérieurs.
- Le cadrage, le PNG et le SVG respectent les deux options de visibilité. Le niveau actif reste visible. Le choix est conservé lors d’un changement de langue pendant la session.
- Format .coat inchangé ; aucune conversion des plans existants nécessaire.

# Coat 1.0 — 22 octobre 2026 — Epicteta Lupercal

- Anglais par défaut, choix English / Français dans les préférences. Application immédiate à l’enregistrement, sans redémarrage.
- Traduction des menus, panneaux, raccourcis, messages, confirmations, aide et indications de la grille avec une terminologie cohérente de level design.
- Conservation des raccourcis existants ; sauvegarde atomique de la langue avec les raccourcis.
- Plan, sélection, vue, historique et palette conservés lors du changement de langue. Textes et noms de groupes des utilisateurs inchangés.
- Guide anglais et exemple d’escaliers légendé en anglais. Format de projet 5 inchangé.

# Coat 0.8 — 22 octobre 2026 — Epicteta Lupercal

- Suppression des 12 couleurs prédéfinies : palette personnelle de 12 cases vides.
- Carré + dans la roue pour enregistrer la couleur dans la première case libre ; clic droit → Supprimer pour libérer une case. Couleurs conservées entre les lancements.
- Pipette dans la roue pour prélever les couleurs visibles du plan et des images de référence, avec aperçu, code hexadécimal et annulation par Échap ou clic droit.
- Escaliers : contours continus et marches au style natif Long dans l’aperçu, le plan et les exports. Styles conservés par l’historique, les transformations et le presse-papiers.
- Format de projet 5 inchangé ; les projets existants gardent leurs styles.

# Coat 0.7 — Epicteta Lupercal

- Panneau Transformations dans Sélection : Translation, Rotation et Échelle, avec icônes vectorielles dédiées.
- Rotation et échelle à la souris autour du centre de la sélection, ou par saisie numérique ; facteurs X/Y indépendants et miroir disponibles.
- Annulation des gestes, historique, couleurs, motifs et groupes conservés.
- Shapes → Escaliers : droits, L, U et colimaçon. Orientation selon le déplacement de la souris, aperçu, nombre de marches, largeur, sens de virage et angle du colimaçon.
- Génération des contours, traits de marches et paliers dans un groupe. Exemple des quatre types fourni.
- Format de projet 5 inchangé. Export PNG limité à la grille et au plan avec échelle.

# Coat 0.6 — 22 octobre 2026 — Epicteta Lupercal

- Copier, couper et coller la sélection via Ctrl+C, Ctrl+X, Ctrl+V et le menu Édition. Presse-papiers Windows, collage au centre de la vue sur le niveau actif, conservation des groupes, couleurs, motifs et annotations, annulation/rétablissement.
- Déplacement d’une forme fermée entièrement sélectionnée depuis sa surface en mode Points ou Arêtes ; déplacement précis et extrusion par arête conservés.
- Affichage optionnel des niveaux supérieurs en traits rouges très fins, via Affichage ou H. Repères non modifiables, sans remplissage ni texte, repris dans les exports.
- Mise à niveau des préférences sans perdre les raccourcis existants.
- Format de projet 5 pour conserver les points indépendants collés ; lecture des formats 1 à 4.

# Coat 0.5.1 — 22 octobre 2026 — Epicteta Lupercal

- Correction de l’export PNG : uniquement la grille et le plan avec l’échelle, sans menus, barre d’outils, panneau latéral ni badge de vue.
- Le cadrage et le zoom affichés sont conservés. L’option d’export de la fenêtre complète est retirée.

# Coat 0.5 — 22 octobre 2026 — Epicteta Lupercal

- Bouton Roue de couleurs dans les outils Couleur et annotations : disque de teinte/saturation, luminosité, hexadécimal, aperçu avant/après, validation ou annulation.
- Export PNG de la fenêtre complète de Coat par défaut, avec une option pour la vue du plan seule dans le champ Type.
- Cadrage et zoom actuels conservés, règles de coordonnées et barre d’échelle incluses dans les deux exports PNG.
- Le document, la sélection et le zoom restent inchangés après l’export ; les surbrillances d’édition ne sont pas imprimées.
- Format de projet inchangé (4).

# Coat 0.4 — Epicteta Lupercal

- Préférences de raccourcis clavier persistantes, validation des doublons et restauration des valeurs par défaut.
- Menus Fichier, Édition, Affichage et Aide blancs avec texte noir.
- Annotations multilignes avec taille, couleur, position, déplacement, duplication, niveaux et annulation.
- Lignes continues, tirets longs ou courts ; styles préservés lors du découpage, de la duplication et des transferts de niveau.
- Remplissages unis, à bandes verticales plus sombres ou petits points, conservés lors du découpage des zones.
- Export PNG/SVG des annotations et motifs. Format de projet 4, lecture des formats 1 à 3.

# Coat — historique des versions

Epicteta Lupercal

## 0.3 — 21 octobre 2026

- Suppression d’un niveau et de son contenu, avec annulation et rétablissement. La navigation saute les niveaux supprimés.
- Opacité progressive des niveaux inférieurs selon leur distance, toujours supérieure à 12 %. Niveaux supérieurs toujours masqués.
- Remplissages du niveau actif à 88 % d’opacité, y compris dans les exports.
- Outil Shapes (`R`) : rectangles de quatre points et cercles réguliers de 3 à 128 points, par deux clics ou cliquer-glisser, avec aperçu.
- Groupes avec `Ctrl+G`, dissociation avec `Ctrl+Maj+G`, sélection et déplacement de l’ensemble, sauvegarde, duplication et affectation de niveau.
- Champs Décalage X/Y conservés après application et lors des changements d’outil ou de sélection pendant la session.
- Aperçu des niveaux plus compact sur les petites fenêtres ; réduction des étiquettes de longueur automatiques pour les grandes sélections.
- Format de projet version 3, avec lecture des projets 0.1 et 0.2.

## 0.2 — 21 octobre 2026

- Grille vide sans message de bienvenue.
- Attribution Epicteta Lupercal dans les sources, les métadonnées, l’en-tête et À propos.
- Échap retire la sélection ou le point de tracé, puis désactive l’outil s’il n’y a plus de sélection.
- État sans outil actif au démarrage ; cliquer à nouveau sur un outil le désactive.
- Suppression de Document dans le panneau de droite. Image de référence et Repères apparaissent uniquement sans outil actif.
- Cadre Niveaux toujours présent, avec aperçu non interactif en trois quarts : fond noir, plans gris, plan actif rouge.
- Navigation entre niveaux, dessin indépendant et affectation des sélections avec leurs couleurs.
- Niveaux inférieurs à 23 % d’opacité, niveaux supérieurs masqués ; édition limitée au niveau actif.
- Sauvegarde des niveaux, migration des anciens plans au niveau 0, annulation et rétablissement des affectations.
- Exports PNG et SVG conformes à la visibilité des niveaux.

## 0.1 — 21 octobre 2026

Première version Windows : grille, dessin aimanté, sélection de points/arêtes/zones, extrusion, gomme, couleurs, image de référence, sauvegarde et exports.
