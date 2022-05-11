import {Component, OnDestroy, OnInit} from '@angular/core';
import {EmrConfigService} from '../../settings/services/emr-config.service';
import {ConfirmationService, Message} from 'primeng/api';
import {Subscription} from 'rxjs/Subscription';
import {EmrSystem} from '../../settings/model/emr-system';
import {BreadcrumbService} from '../../app/breadcrumb.service';
import {Extract} from '../../settings/model/extract';
import {CrsService} from '../services/crs.service';
import {DatabaseProtocol} from '../../settings/model/database-protocol';
import {ExtractPatient} from '../ndwh-docket/model/extract-patient';
import {HubConnection, HubConnectionBuilder, LogLevel} from '@aspnet/signalr';
import {ExtractEvent} from '../../settings/model/extract-event';
import {ClientRegistryExtract} from '../models/client-registry-extract';
import {RegistryConfigService} from '../../settings/services/registry-config.service';
import {CentralRegistry} from '../../settings/model/central-registry';
import {SendPackage} from '../../settings/model/send-package';
import {SendResponse} from '../../settings/model/send-response';
import {SendEvent} from '../../settings/model/send-event';
import {environment} from '../../environments/environment';
import {EmrMetrics} from '../../settings/model/emr-metrics';

@Component({
    selector: 'liveapp-crs-docket',
    templateUrl: './crs-docket.component.html',
    styleUrls: ['./crs-docket.component.scss']
})
export class CrsDocketComponent implements OnInit, OnDestroy {

    private _hubConnection: HubConnection | undefined;
    public async: any;

    private _confirmationService: ConfirmationService;
    private _emrConfigService: EmrConfigService;

    public getEmr$: Subscription;
    public load$: Subscription;
    public getStatus$: Subscription;
    public get$: Subscription;
    public getCount$: Subscription;
    public getall$: Subscription;
    public getallCount$: Subscription;
    public loadRegistry$: Subscription;
    public sendManifest$: Subscription;
    public send$: Subscription;
    public emrSystem: EmrSystem;
    public emrMetric: EmrMetrics;
    public emrVersion: string;
    public minEMRVersion: string;
    public extracts: Extract[];
    public dbProtocol: DatabaseProtocol;
    public extract: Extract;
    public extractPatient: ExtractPatient;
    private extractEvent: ExtractEvent;
    public sendEvent: SendEvent = {};
    public extractDetails: ClientRegistryExtract[] = [];
    public allExtractDetails: ClientRegistryExtract[] = [];
    public sendResponse: SendResponse;
    public manifestPackage: SendPackage;
    public crsPackage: SendPackage;


    public messages: Message[];
    public metricMessages: Message[];
    public notifications: Message[];
    public canLoad: boolean = false;
    public loading: boolean = false;
    public loadingAll: boolean = false;
    public canSend: boolean = false;
    public canSendCrs: boolean = false;
    public sending: boolean = false;
    public sendingManifest: boolean = false;
    public recordCount = 0;
    public allrecordCount = 0;
    private sdk: string[] = [];
    public colorMappings: any[] = [];
    rowStyleMap: { [key: string]: string };
    public centralRegistry: CentralRegistry;

    public constructor(public breadcrumbService: BreadcrumbService,
                       confirmationService: ConfirmationService, emrConfigService: EmrConfigService, private CrsService: CrsService,
                       private _registryConfigService: RegistryConfigService) {
        this.breadcrumbService.setItems([
            {label: 'Dockets'},
            {label: 'Client Registry Service', routerLink: ['/Crs']}
        ]);
        this._confirmationService = confirmationService;
        this._emrConfigService = emrConfigService;
    }

    public ngOnInit() {
        this.loadRegisrty();
        this.loadData();
        this.liveOnInit();
        this.loadDetails();
    }
    private updateExractStats(dwhProgress: any) {
        if(dwhProgress) {
            this.extracts.map(e => {
                    if (e.name === dwhProgress.extract && e.extractEvent) {
                        e.extractEvent.sent = dwhProgress.sent;
                    }
                }
            );
        }
    }
    private liveOnInit() {
        this._hubConnection = new HubConnectionBuilder()
            .withUrl(`${window.location.protocol}//${document.location.hostname}:${environment.port}/CrsActivity`)
            .configureLogging(LogLevel.Error)
            .build();
        this._hubConnection.serverTimeoutInMilliseconds = 120000;

        this._hubConnection.start().catch(err => console.error(err.toString()));

        this._hubConnection.on('ShowCrsProgress', (dwhProgress: any) => {

            if (this.extract) {
                this.extractEvent = {
                    lastStatus: `${dwhProgress.status}`, found: dwhProgress.found, loaded: dwhProgress.loaded,
                    rejected: dwhProgress.rejected, queued: dwhProgress.queued, sent: dwhProgress.sent
                };
                this.extract.extractEvent = {};
                this.extract.extractEvent = this.extractEvent;
                const newWithoutPatientExtract = this.extracts.filter(x => x.name !== 'ClientRegistryExtract');
                this.extracts = [...newWithoutPatientExtract, this.extract];
            }
        });

        this._hubConnection.on('ShowCrsSendProgress', (dwhProgress: any) => {
            if (this.extract) {
                this.sendEvent = {
                    sentProgress: dwhProgress.progress
                };
                this.updateExractStats(dwhProgress);
                this.sending = true;
                this.canLoad = this.canSend = !this.sending;
            }
        });
    }

    public loadData(): void {

        this.canLoad = false;

        this.getEmr$ = this._emrConfigService.getDefault()
            .subscribe(
                p => {
                    this.emrSystem = p;
                },
                e => {
                    this.messages = [];
                    this.messages.push({severity: 'error', summary: 'Error Loading data', detail: <any>e});
                },
                () => {
                    this.loadMetrics();
                    if (this.emrSystem) {
                        this.emrVersion = this.emrSystem.version;
                        const em=environment.emrs.filter(x=>x.name===this.emrSystem.name)[0];
                        if (this.emrSystem.name == 'KenyaEMR') {
                            this.minEMRVersion = `(This version of DWAPI works best with ${this.emrSystem.name} version ${em.version}) or higher`;
                        }
                        else if (this.emrSystem.name === 'IQCare') {
                            this.minEMRVersion = `(This version of DWAPI works best with ${this.emrSystem.name} version ${em.version}) or higher`;
                        }
                        else {
                            this.minEMRVersion = '';
                        }

                        if (this.emrSystem.extracts) {
                            this.extracts = this.emrSystem.extracts.filter(x => x.docketId === 'CRS');

                            this.extract = this.extracts[0];
                            this.dbProtocol = this.emrSystem.databaseProtocols.find(x => x.id === this.extract.databaseProtocolId);
                            if (this.extract && this.dbProtocol) {
                                this.canLoad = true;
                                this.updateEvent();
                            }
                        }
                    }
                }
            );
    }

    public loadMetrics(): void {

        this.getEmr$ = this._emrConfigService.loadMetrics(this.emrSystem)
            .subscribe(
                p => {
                    this.emrMetric = p;
                },
                e => {
                    this.metricMessages = [];
                    // this.metricMessages.push({severity: 'warn', summary: 'Could not load EMR metrics', detail: <any>e});
                },
                () => {
                    if (this.emrMetric) {
                        this.emrVersion = this.emrMetric.emrVersion;
                    }
                }
            );

    }

    public loadRegisrty(): void {
        this.messages = [];
        this.loadRegistry$ = this._registryConfigService.get('CRS').subscribe(
            p => {
                this.centralRegistry = p;
            },
            e => {
                this.messages = [];
                this.messages.push({
                    severity: 'error',
                    summary: 'Error loading regisrty ',
                    detail: <any>e
                });
            },
            () => {
                if (this.centralRegistry) {
                    this.canSend = true;
                }
            }
        );
    }

    public loadFromEmr(): void {
        this.extractDetails = [];
        this.messages = [];
        this.extractPatient = {extract: this.extract, databaseProtocol: this.dbProtocol};
        this.load$ = this.CrsService.extract(this.extractPatient)
            .subscribe(
                p => {
                    // this.isVerfied = p;
                },
                e => {
                    this.messages = [];
                    this.messages.push({severity: 'error', summary: 'Error loading ', detail: <any>e});
                },
                () => {
                    this.messages = [];
                    this.messages.push({severity: 'success', summary: 'load was successful '});
                    this.updateEvent();
                    this.loadDetails();
                }
            );


    }

    public updateEvent(): void {


        if (!this.extract) {
            return;
        }

        this.getCount$ = this.CrsService.getDetailCount()
            .subscribe(
                p => {
                    this.recordCount = p;
                },
                e => {
                    this.messages = [];
                    this.messages.push({severity: 'error', summary: 'Error loading status ', detail: <any>e});
                },
                () => {
                }
            );

        this.getallCount$ = this.CrsService.getAllDetailCount()
            .subscribe(
                p => {
                    this.allrecordCount = p;
                },
                e => {
                    this.messages = [];
                    this.messages.push({severity: 'error', summary: 'Error loading status ', detail: <any>e});
                },
                () => {

                }
            );
        this.getStatus$ = this.CrsService.getStatus(this.extract.id)
            .subscribe(
                p => {
                    this.extract.extractEvent = p;
                    if (this.extract) {
                        if (this.extract.extractEvent) {
                            this.canSend = this.extract.extractEvent.queued > 0;
                        }
                    }
                },
                e => {
                    this.messages = [];
                    this.messages.push({severity: 'error', summary: 'Error loading status ', detail: <any>e});
                },
                () => {

                }
            );

    }


    public send(): void {
        this.sendingManifest = true;

        this.messages = [];
        this.notifications = [];
        this.canSendCrs = false;
        this.manifestPackage = this.getSendManifestPackage();
        this.sendManifest$ = this.CrsService.sendManifest(this.manifestPackage)
            .subscribe(
                p => {
                    this.canSendCrs = true;
                },
                e => {
                    this.messages = [];
                    this.messages.push({severity: 'error', summary: 'Error sending ', detail: <any>e});
                },
                () => {
                    //  this.notifications.push({severity: 'success', summary: 'Manifest sent'});
                    this.sendCrs();
                    this.sendingManifest = false;
                    this.updateEvent();
                }
            );
    }

    public sendCrs(): void {
        this.sendEvent = {sentProgress: 0};
        this.sending = true;
        this.messages = [];
        this.crsPackage = this.getCrsPackage();
        this.send$ = this.CrsService.sendCrs(this.crsPackage)
            .subscribe(
                p => {
                    // this.sendResponse = p;
                },
                e => {
                    this.messages = [];
                    this.messages.push({severity: 'error', summary: 'Error sending ', detail: <any>e});
                },
                () => {
                    this.messages.push({severity: 'success', summary: 'sent successfully '});
                    this.sending = false;
                    this.updateEvent();
                }
            );
    }

    private getSendManifestPackage(): SendPackage {
        return {
            extractId: this.extract.id,
            destination: this.centralRegistry,
            emrSetup: this.emrSystem.emrSetup,
            emrId:this.emrSystem.id,
            emrName:this.emrSystem.name
        };
    }

    private getCrsPackage(): SendPackage {
        return {
            destination: this.centralRegistry,
            extractId: this.extract.id,
        };
    }


    private isEven(value: number): boolean {
        if ((value % 2) !== 0) {
            return false;
        }
        return true;
    }

    private loadDetails(): void {
        this.loadingAll = this.loading = true;
        this.get$ = this.CrsService.getDetails()
            .subscribe(
                p => {
                    this.extractDetails = p;
                },
                e => {
                    this.messages = [];
                    this.messages.push({severity: 'error', summary: 'Error Loading data', detail: <any>e});
                },
                () => {
                    this.loading = false;

                    this.sdk = Array.from(new Set(this.extractDetails.map(extract => extract.sxdmPKValueDoB)));
                    this.colorMappings = this.sdk.map((sd, idx) => ({sxdmPKValueDoB: sd, color: this.isEven(idx) ? 'white' : 'pink'}));
                }
            );

        this.getall$ = this.CrsService.getAllDetails()
            .subscribe(
                p => {
                    this.allExtractDetails = p;
                },
                e => {
                    this.messages = [];
                    this.messages.push({severity: 'error', summary: 'Error Loading data', detail: <any>e});
                },
                () => {
                    this.loadingAll = false;

                    this.sdk = Array.from(new Set(this.extractDetails.map(extract => extract.sxdmPKValueDoB)));
                    this.colorMappings = this.sdk.map((sd, idx) => ({sxdmPKValueDoB: sd, color: this.isEven(idx) ? 'white' : 'pink'}));
                }
            );
    }

    lookupRowStyleClass(rowData: ClientRegistryExtract) {
        // console.log(rowData);
        return rowData.sxdmPKValueDoB === 'FA343ALPS19730615' ? 'disabled-account-row' : '';

    }

    public ngOnDestroy(): void {
        if (this.getEmr$) {
            this.getEmr$.unsubscribe();
        }
        if (this.getStatus$) {
            this.getStatus$.unsubscribe();
        }
        if (this.load$) {
            this.load$.unsubscribe();
        }
        if (this.get$) {
            this.get$.unsubscribe();
        }
        if (this.getCount$) {
            this.getCount$.unsubscribe();
        }
        if (this.getallCount$) {
            this.getallCount$.unsubscribe();
        }
        if (this.getall$) {
            this.getall$.unsubscribe();
        }
        if (this.loadRegistry$) {
            this.loadRegistry$.unsubscribe();
        }
        if (this.send$) {
            this.send$.unsubscribe();
        }
        if (this.sendManifest$) {
            this.sendManifest$.unsubscribe();
        }
    }
}
