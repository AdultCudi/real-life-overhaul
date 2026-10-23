import json
from pathlib import Path
out=Path('contracts/companion/v1')
I={'type':'integer','minimum':-9007199254740991,'maximum':9007199254740991}
N={'type':'integer','minimum':0,'maximum':9007199254740991}
S={'type':'string','minLength':1,'maxLength':1024}
B={'type':'boolean'}
D={'type':'string','pattern':r'^-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[Ee][+-]?[0-9]+)?$','maxLength':64}
def ar(t):return {'type':'array','maxItems':10000,'items':t}
def ob(**p):return {'type':'object','properties':p,'required':list(p),'additionalProperties':False}
def en(*v):return {'enum':list(v)}
def nu(t):return {'anyOf':[t,{'type':'null'}]}
def fields(names,t=I):return {k:t for k in names.split()}
A=ob(street=S,number=N)
mod={}
uterms=json.loads((out/'utilities-vectors.json').read_text())['sample']['terms']
# This schema follows the emitted owner model, preserving every numerical unit in its field name.
def observed_schema(value):
 if isinstance(value,bool):return B
 if isinstance(value,int):return I
 if isinstance(value,str):return {'const':value}
 if isinstance(value,list):return ar(observed_schema(value[0]) if value else {})
 if isinstance(value,dict):return ob(**{k:observed_schema(v) for k,v in value.items()})
 return {'type':'null'}
account=ob(address=A,service=en('power','water','internet'),state=en('pending','active','temporary','cut','closed'),owned=B,
 **fields('accruedCents fromDay lastDay owedCents dueDay lastBillCents lastBillDay depositCents connectDay temporaryUntilDay provider plan contractUntilDay monthCents outageFromHour outageUntilHour'),businessPlan=B)
plan=ob(**fields('plan mbps businessContractMonthCents businessMonthlyMonthCents'))
internet=ob(providers=ar(ob(provider=I,id=S,districts=ar(S),plans=ar(plan))),**fields('installDays installFeeCents contractInstallFeeCents contractDays'),needs=ar(S),futureOutageAssumption=en('no-outage-days'),dailyRounding=S,accrualIncludesOtherProviderSeatCharges=B)
mod['utilities']=ob(model=en('rlo-utilities-v1'),terms=observed_schema(uterms),typeKinds=ar(ob(typeId=S,kind=I)),accounts=ar(account),unresolvedAccountServices=ar(S),internet=internet,internetEnabled=B,graceUntilDay=I,graceAddresses=ar(A),closureConditions=ar(S))
mod['prices']=ob(model=en('rlo-prices-v1'),wholesaleBasis=en('item-field-before-market-index-and-multiplier'),defaultMarketBasis=en('item-field-before-market-index-and-multiplier'),defaultMarketSource=en('live-registered-item'),**fields('levelThousandths quietBand adjustmentQuiet costFactorThousandths takingsFactorThousandths'),
 items=ar(ob(itemId=S,ownAmount=D,effectiveAmount=D,kind=I,factorThousandths=I,exchangeFactor=nu(D),provenance=en('material-exchange','city-kind','registered-current'),defaultMarketAmount=D)),livingTransactionTypes=ar(S),livingExcludesWholesalePurchases=B,adjustmentRounding=S,statementAdjustment=en('already-included'),depositAdjustment=nu(en('none','rentCostFactor')),depositAdjustmentBasis=S,depositRefundAdjustment=en('none'),carriedDebtInSiteForecast=B)
policy=ob(**fields('kind insurer dailyCents fromDay untilDay claims'),active=B,quoteInputs=ob(**fields('averageTakingsCents income index carriedClaims')),quotes=ar(ob(insurer=I,eligible=B,dayCents=N)))
lic=ob(kind=I,untilDay=I,valid=B,autoRenew=B)
site=ob(address=A,raises=ar(ob(employeeId=S,raiseHourlyCents=I)),sickPayPolicy=I,managerDailyAllowanceCents=N,overtime=ob(active=B,**fields('fromDay untilDay dailyCents')),cover=ar(policy),licences=ar(lic),hiddenCashShareThousandths=I,campaign=ob(kind=I,active=B,**fields('fromDay untilDay paidCents percent discountGivenCents')))
mod['company']=ob(partStates=ob(**fields('staff cover marketing tax licences',en('ready','disabled','failed'))),model=en('rlo-company-v1'),sites=ar(site),parts=ob(**fields('staff cover marketing tax licences',B)),newHireWageFactorThousandths=I,sickShareThousandthsByPolicy=ar(I),sickFallbackDayHours=I,newSiteDefaults=ob(raiseHourlyCents=I,sickPayPolicy=I,cover=en('none'),campaign=en('none'),hiddenCashShareThousandths=I),accountant=ob(**fields('tier monthlyFeeCents nextDay monthDays')),coverTerms=ob(baseMoney=ar(I),takingsShareThousandths=ar(I),insurerPriceThousandths=ar(I),incomePerPoint=ar(I),**fields('districtMinThousandths districtMaxThousandths claimLoadingThousandths termDays')),statementInclusion=ar(ob(costKind=I,costName=en('wages','premiums','marketing','licences','accountant'),statementCategory=nu(S))))
mod['requirements']=ob(model=en('rlo-opening-requirements-v1'),types=ar(ob(typeId=S,licenceNeedsMask=I)),licences=ar(ob(**fields('kind feeCents termDays'),closesWhenMissing=B)),lateOpeningHours=ob(**fields('before after permitKind')),permitValidity=en('untilDay-inclusive'),newSiteGraceDays=I,paymentBasis=en('startup-then-term-renewal'),internetNeeds=ar(S))
quote=ob(mode=I,loan=B,immediateCashCents=N,totalFeeCents=N,installmentsTotal=I,installments=ar(ob(index=I,afterSessions=I,amountCents=N)),futurePriceBasis=en('captured-level'),quoteStatus=en('ok','ineligible','unknown'),reason=nu(S),**fields('feeNowCents grantCents grantThousandths retakeCents approximateDurationDays daysMask startHour'))
enrollment=ob(status=S,**fields('paidCents loanCents installmentsPaid installmentsTotal owedCents startDay mode'),onLoan=B)
mod['education']=ob(model=en('rlo-education-v1'),quoteBasis=en('captured-settings-cash-and-loan-room'),courses=ar(ob(courseId=I,offered=B,qualified=B,prerequisitesMet=B,enrollment=nu(enrollment),quotes=ar(quote),timetable=ob(**fields('daysMask startHour hours sessions homework')))),loanOwedCents=N,loanRateBasisPointsPerYear=I,completionGuarantee=B)
tolerance=ob(marketSource=en('city-find-goods-captured-item'),marketBasis=en('item-field-before-market-index-and-multiplier'),districts=ar(ob(districtId=S,priceIndex=nu(D),**fields('toleranceThousandths cyclePercent districtPercent'))),items=ar(ob(itemId=S,marketAmount=D)),monopolies=ar(ob(itemId=S,districtId=S)),**fields('bearsMinThousandths bearsMaxThousandths monopolyAllowanceThousandths overBandThousandths underGainPercent overLossPerPercent overLossMaxPercent footfallMinPercent footfallMaxPercent footfallQuietPercent'),ratioRounding=S)
mod['footfall']=ob(terms=ob(**fields('issueMin issueMax ambientMin ambientMax interiorMin interiorMax promotionMin promotionMax'),promotionCoefficient=D,rounding=en('float32-add-half-truncate')),model=en('rlo-footfall-v1'),sites=ar(ob(address=A,status=en('ready','unavailable'),**fields('ownPromotion writtenPromotion registrationPromotionAtCapture issuePercent ambientPercent interiorPercent',nu(I)),cityPercent=I,companyPercent=I,baseMultiplier=nu(D))),priceTolerance=nu(tolerance),futureAssumption=en('captured-city-settings-held-constant'))
iterms=ob(**fields('standardScaleThousandths decorMille priceScaleThousandths interiorShareThousandths shortPointsPerPercent shortFootfallMaxPercent overPointsPerPercent overFootfallMaxPercent bearsOverMaxThousandths basketFullAt basketMostPoorThousandths basketMostRichThousandths standardLeast standardMost graceDays rampDays repeatShareThousandths repeatMost targetLeast targetFromSqm targetSqmPerPoint targetMost varietyFrom varietyMost incomeLeast incomeMost footfallLeast footfallMost maxDecorMille officeBoostMost basketOfficeMost'),basket=B,categoryIds=ar(S),itemPoints=ar(I),capShareThousandths=ar(I))
mod['interiors']=ob(model=en('rlo-interiors-v1'),districts=ar(ob(districtId=S,**fields('income nativeMinimum requiredScore'))),sites=ar(ob(address=A,**fields('overall decor interior requiredScore footfallPercent priceToleranceThousandths basketThousandths rampThousandths standardFromDay officeBoostThousandths'),fitting=B,saleStop=S)),items=ar(ob(itemId=S,category=I,seat=B,table=B,kitchen=B)),terms=iterms,hypotheticalDecorCoverage=en('catalogue-items'))
mod['closures']=ob(rows=ar(ob(address=A,source=S,fromDay=nu(I),untilDay=I,untilHour=I,end=en('observed-hold'),mayRenew=B)),futureEvents=en('risk-not-predicted'),coverage=en('owner-holds'))
mod['bankTerms']=ob(model=en('rlo-native-bank-terms-v1'),quoteBasis=en('captured-rating-and-settings'),banks=ar(ob(bankId=S,address=A,capCents=N,annualRatePercent=D,termDays=I,debtBasis=en('native-loans'),wealthBasis=en('native-wealth-before-loans'),profitBasis=en('native-statement-daily-income'),incomeTermShareThousandths=I,securedAccountRulesApply=B,securedCapTreatment=en('included-in-effective-cap'),tutorialMinimumCents=N,nativeDebtAtBankCents=N)),additionalCreditIncluded=B,principalIsExpense=B)
mod['outsideStatements']=ob(rows=ar(ob(scope=en('place','company'),address=nu(A),category=en('utilities.power','utilities.water','utilities.internet','cover.premium','cover.excess','tax.accountant','tax.penalty','tax.rebate','tax.backtax','racket','repair','other'),basis=en('paid','accrued','owed'),fromDay=I,toDay=I,cents=I)),coverage=en('partial','complete','unavailable'),knownOmittedCategories=ar(S),recurringDeductionLabels=ar(ob(label=S,kind=en('recurring','once'))),unclassifiedDeductionLabels=ar(S),balancesAreSeparate=B,taxEffectsOutsideNativeForecast=ar(S))
decision=ob(schemaVersion={'const':1},decision=en('allowed','conditional','refused','unknown'),owner=S,task=S,reasons=ar(S),effects=ar(S))
mod['ownership']=ob(targets=ar(ob(address=A,operations=ob(schedule=decision,hire=decision,wholesale=decision,rent=decision))),authorization=en('live-query-required'),managersEnabled=B,estateEnabled=B)
wrapped={k:{'oneOf':[ob(version={'const':1},status={'const':'ready'},data=v),ob(version={'const':1},status=en('disabled','unavailable','failed'),data={'type':'null'})]} for k,v in mod.items()}
schema={'$schema':'https://json-schema.org/draft/2020-12/schema','$id':'https://reallifeoverhaul.dev/contracts/companion/v1/planning.schema.json','title':'RLO additive planning modules, version 1','type':'object','properties':wrapped,'additionalProperties':True,'$defs':mod}
(out/'planning.schema.json').write_text(json.dumps(schema,indent=2)+'\n')
(out/'ownership-response.schema.json').write_text(json.dumps({'$schema':'https://json-schema.org/draft/2020-12/schema',**decision},indent=2)+'\n')
# Ownership is deliberately separate from advisory snapshot data.
hour=ob(**fields('from to',{'type':'integer','minimum':0,'maximum':24}))
shift=ob(**hour['properties'],employeeId=S,itemInstanceId=nu(S))
week=ar(ob(day={'type':'integer','minimum':0,'maximum':6},isOpen=B,hours=ar(hour),shifts=ar(shift)))
week.update(minItems=7,maxItems=7)
schedule=ob(**A['properties'],week=week)
hire=ob(**A['properties'],hiring=B,moving=B,scheduling=B,week=week)
hire['required'].remove('week')
hire['allOf']=[{'if':{'properties':{'scheduling':{'const':True}}},'then':{'required':['week']}}]
wholesale=ob(**A['properties'],contractId=S,action=en('set','remove'),itemIds=ar(S))
operations=[]
for name,target in [('schedule',schedule),('hire',hire),('wholesale',wholesale),('rent',A)]:
 targets=ar(target);targets.update(minItems=1,maxItems=256)
 operations.append(ob(schemaVersion={'const':1},operation={'const':name},phase=en('review','apply','undo'),targets=targets))
(out/'ownership-request.schema.json').write_text(json.dumps({'$schema':'https://json-schema.org/draft/2020-12/schema','oneOf':operations},indent=2)+'\n')
