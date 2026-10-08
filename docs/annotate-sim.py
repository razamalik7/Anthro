"""
Adds the machine-readable "sim" block to each discoverable stage in discovery-web.json.

The free-text needs ("smooth river stones", "food short, water close") stay for people to read;
the sim block says the same things in tags the simulation can check:
  during     activities a person might stumble on it while doing (Verb names)
  materials  things that must be at hand near camp (see Inventing.cs for the tag list)
  pressures  situations that push people toward it (raise the chance; at least one helps a lot)
  interest   the interest that makes someone pay attention to it
  skill      the skill whose practice makes it likelier
  trait      a mind trait that helps (e.g. Patience, Boldness)
  way        a way of life it needs: "settled" (two weeks or more at a camp), "settled-long" (a season)
  rate       relative ease (1 = typical)
Knowledge prerequisites come from the refs already in "needs".

Run: python docs/annotate-sim.py  (then python docs/build-spec.py to rebuild the page)
"""
import json, os

HERE = os.path.dirname(os.path.abspath(__file__))
PATH = os.path.join(HERE, 'discovery-web.json')

S = {
    'fire.hearth':   dict(during=['Cook', 'Rest'], way='settled', interest='Fire', skill='Fire'),
    'fire.hot':      dict(during=['Cook', 'Knap'], materials=['forest'], interest='Fire', skill='Fire'),
    'fire.land':     dict(during=['Forage', 'Hunt'], materials=['grassland'], pressures=['flight'], interest='Fire', trait='Boldness'),
    'fire.torch':    dict(during=['Rest', 'Socialize'], materials=['forest'], pressures=['dark', 'danger'], interest='Fire'),
    'fire.lamp':     dict(during=['Cook'], materials=['fat'], interest='Fire'),
    'knap.haft':     dict(during=['Knap'], skill='Knapping', interest='StoneEarth'),
    'knap.heat':     dict(during=['Knap'], materials=['flint'], interest='StoneEarth', trait='Curiosity'),
    'bone.awl':      dict(during=['Knap', 'Cook'], materials=['bone'], interest='Animals', trait='Patience'),
    'bone.point':    dict(during=['Knap'], materials=['bone'], interest='Animals'),
    'bone.needle':   dict(during=['Knap', 'Rest'], materials=['bone'], pressures=['cold'], skill='Knapping'),
    'bone.hook':     dict(during=['Fish', 'Knap'], materials=['bone', 'river'], interest='Water'),
    'bone.harpoon':  dict(during=['Fish'], materials=['bone', 'river'], interest='Water'),
    'wood.axe':      dict(during=['GatherWood', 'Knap'], materials=['hills'], pressures=['cold']),
    'wood.polish':   dict(during=['Knap'], way='settled-long', trait='Patience', interest='Wood'),
    'wood.canoe':    dict(during=['Fish'], materials=['river', 'forest'], interest='Water', trait='Boldness', rate=0.5),
    'spear.stone':   dict(during=['Knap', 'Hunt'], skill='Knapping', interest='StoneEarth'),
    'spear.light':   dict(during=['Hunt'], materials=['forest'], pressures=['flight'], skill='Hunting'),
    'trap.trail':    dict(during=['Hunt', 'Forage'], interest='Animals', trait='Patience'),
    'trap.snare':    dict(during=['Forage', 'Hunt'], materials=['game'], pressures=['hunger'], interest='Animals'),
    'trap.dead':     dict(during=['Hunt'], pressures=['danger'], interest='Animals', trait='Patience'),
    'trap.spring':   dict(during=['Forage'], materials=['forest'], trait='Curiosity'),
    'forage.lore':   dict(during=['Forage', 'Socialize'], interest='Plants', skill='Foraging'),
    'forage.nuts':   dict(during=['Forage'], materials=['nuts', 'river'], pressures=['hunger'], trait='Patience'),
    'lang.story':    dict(during=['Socialize'], pressures=['winter', 'death'], trait='Sociability'),
    'lang.ritual':   dict(during=['Socialize'], pressures=['death'], interest='Spirits'),
    'lang.count':    dict(during=['Preserve', 'Socialize'], trait='Patience'),
    'cord.sinew':    dict(during=['Knap', 'Cook'], materials=['sinew'], interest='Animals'),
    'cord.twine':    dict(during=['Forage'], materials=['reeds'], pressures=['carry'], interest='Plants'),
    'cord.ply':      dict(during=['Rest', 'Knap']),
    'cord.rope':     dict(during=['Rest', 'GatherWood']),
    'cord.net':      dict(during=['Fish', 'Rest'], materials=['river'], trait='Patience'),
    'cook.hotstone': dict(during=['Cook'], materials=['river'], interest='Hearth', skill='Cooking', trait='Patience'),
    'cook.boil':     dict(during=['Cook'], interest='Hearth', skill='Cooking'),
    'cook.grease':   dict(during=['Cook'], materials=['bone'], pressures=['hunger', 'winter'], interest='Hearth'),
    'cook.oven':     dict(during=['Cook'], materials=['grassland'], interest='Hearth'),
    'cook.bread':    dict(during=['Cook'], interest='Hearth'),
    'cook.pot':      dict(during=['Cook'], interest='Hearth'),
    'dry.keep':      dict(during=['Cook', 'Eat'], materials=['glut'], pressures=['spoilage'], interest='Hearth'),
    'dry.rack':      dict(during=['Preserve'], materials=['forest'], pressures=['winter', 'spoilage'], interest='Hearth'),
    'dry.smoke':     dict(during=['Preserve'], materials=['glut'], trait='Patience', interest='Hearth'),
    'dry.pemmican':  dict(during=['Preserve'], materials=['fat'], interest='Hearth'),
    'atl.hook':      dict(during=['Hunt', 'Knap'], pressures=['flight'], skill='Hunting'),
    'atl.dart':      dict(during=['Hunt', 'Knap'], skill='Hunting'),
    'grind.pound':   dict(during=['Cook', 'Forage'], materials=['nuts'], interest='Plants'),
    'grind.mortar':  dict(during=['Cook'], way='settled', interest='Plants'),
    'grind.slab':    dict(during=['Cook'], way='settled-long', interest='Plants'),
    'dog.lurk':      dict(during=['Rest', 'Cook'], materials=['wolves']),
    'dog.pup':       dict(during=['Care', 'Play'], materials=['wolves'], interest='Animals'),
    'dog.camp':      dict(during=['Care'], interest='Animals', rate=0.5),
    'dog.hunt':      dict(during=['Hunt'], skill='Hunting', interest='Animals'),
    'burial.cover':  dict(during=['Care', 'Rest'], pressures=['death']),
    'burial.goods':  dict(during=['Care', 'Socialize'], pressures=['death'], interest='Spirits'),
    'burial.rites':  dict(during=['Socialize'], pressures=['death'], interest='Spirits'),
    'ochre.body':    dict(during=['Socialize'], materials=['ochre'], interest='Spirits'),
    'ochre.hand':    dict(during=['Rest'], interest='Art'),
    'ochre.paint':   dict(during=['Rest'], interest='Art'),
    'ochre.figure':  dict(during=['Knap'], interest='Art'),
    'bask.bundle':   dict(during=['Forage'], materials=['reeds'], pressures=['carry'], interest='Plants'),
    'bask.woven':    dict(during=['Forage', 'Rest'], interest='Plants', trait='Patience'),
    'bask.tight':    dict(during=['Forage', 'Rest'], materials=['grain'], interest='Plants'),
    'bask.sealed':   dict(during=['Rest', 'Cook'], materials=['forest'], interest='Plants'),
    'hide.wrap':     dict(during=['Rest', 'Cook'], materials=['hide'], pressures=['cold']),
    'hide.scrape':   dict(during=['Cook', 'Rest'], materials=['hide'], trait='Patience'),
    'hide.awl':      dict(during=['Rest'], materials=['hide']),
    'hide.tent':     dict(during=['Rest'], materials=['hide', 'forest'], pressures=['cold']),
    'hide.needle':   dict(during=['Rest'], materials=['hide'], pressures=['cold']),
    'hide.shoe':     dict(during=['Forage', 'Hunt'], materials=['hide'], pressures=['cold']),
    'hide.snowshoe': dict(during=['Hunt', 'GatherWood'], materials=['snow'], pressures=['winter']),
    'shelter.cave':  dict(during=['Forage', 'Hunt'], materials=['cave'], pressures=['cold']),
    'shelter.lean':  dict(during=['Rest'], materials=['forest'], pressures=['cold']),
    'shelter.hut':   dict(during=['Rest'], materials=['forest'], pressures=['cold'], way='settled'),
    'shelter.pit':   dict(during=['Rest'], pressures=['cold', 'winter'], way='settled-long'),
    'fish.spear':    dict(during=['Fish'], materials=['river'], pressures=['hunger'], interest='Water'),
    'fish.weir':     dict(during=['Fish'], materials=['fishrun', 'forest'], trait='Patience', interest='Water'),
    'fish.trap':     dict(during=['Fish'], materials=['river'], interest='Water'),
    'fish.line':     dict(during=['Fish'], materials=['river'], interest='Water'),
    'fish.net':      dict(during=['Fish'], materials=['river'], interest='Water'),
    'bow.simple':    dict(during=['Hunt', 'Knap'], materials=['forest'], pressures=['flight'], skill='Hunting', trait='Curiosity'),
    'bow.fletch':    dict(during=['Knap', 'Hunt'], skill='Hunting'),
    'bow.stone':     dict(during=['Knap'], skill='Knapping'),
    'bow.backed':    dict(during=['Knap'], skill='Hunting'),
    'store.cache':   dict(during=['Forage', 'Preserve', 'Cook'], pressures=['spoilage', 'winter'], trait='Patience'),
    'store.pit':     dict(during=['Preserve', 'Forage'], way='settled'),
    'store.basket':  dict(during=['Preserve', 'Forage']),
    'store.raised':  dict(during=['Preserve'], pressures=['danger']),
    'store.cold':    dict(during=['Preserve', 'Hunt'], materials=['snow'], pressures=['winter']),
    'camp.return':   dict(during=['Socialize', 'Forage'], interest='Plants'),
    'camp.long':     dict(during=['Socialize'], way='settled'),
    'camp.year':     dict(during=['Socialize'], way='settled-long'),
    'pot.fired':     dict(during=['Cook'], materials=['clay'], interest='StoneEarth'),
    'pot.pinch':     dict(during=['Cook'], materials=['clay'], way='settled-long', interest='Fire'),
    'pot.coil':      dict(during=['Cook'], materials=['clay'], trait='Patience'),
    'pot.pit':       dict(during=['Cook'], materials=['clay']),
    'grain.harvest': dict(during=['Forage'], materials=['grain'], pressures=['hunger'], interest='Plants'),
    'grain.clear':   dict(during=['Forage'], materials=['grain'], interest='Plants'),
    'grain.scatter': dict(during=['Forage'], materials=['grain'], interest='Plants'),
    'grain.select':  dict(during=['Forage'], materials=['grain'], trait='Patience', interest='Plants'),
    'wayfind.names': dict(during=['Socialize', 'Forage'], trait='Sociability'),
    'wayfind.sketch': dict(during=['Socialize'], trait='Curiosity'),
    'wayfind.carry': dict(during=['Rest'], materials=['hide'], trait='Patience'),
    'wayfind.stars': dict(during=['Rest', 'Visit'], interest='Sky'),
    'hunt.surround': dict(during=['Hunt'], materials=['grassland'], pressures=['flight'], skill='Hunting'),
    'hunt.persist':  dict(during=['Hunt'], pressures=['flight'], trait='Boldness', rate=0.7),
}

w = json.load(open(PATH, encoding='utf-8'))
missing = []
for t in w['tiles']:
    for s in t['stages']:
        if s.get('known'):
            s.pop('sim', None)
            continue
        if s['id'] not in S:
            missing.append(s['id'])
            continue
        s['sim'] = S[s['id']]
json.dump(w, open(PATH, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
print('annotated', sum(1 for t in w['tiles'] for s in t['stages'] if 'sim' in s), 'stages; missing:', missing)
