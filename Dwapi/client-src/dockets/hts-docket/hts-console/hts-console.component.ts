import {Component, Input, OnChanges, OnDestroy, OnInit, SimpleChange} from '@angular/core';
import {EmrSystem} from '../../../settings/model/emr-system';
import {HubConnection, HubConnectionBuilder, LogLevel} from '@aspnet/signalr';
import {ConfirmationService, Message} from 'primeng/api';
import {HtsService} from '../../services/hts.service';
import {RegistryConfigService} from '../../../settings/services/registry-config.service';
import {HtsSenderService} from '../../services/hts-sender.service';
import {Subscription} from 'rxjs/Subscription';
import {Extract} from '../../../settings/model/extract';
import {DwhExtract} from '../../../settings/model/dwh-extract';
import {ExtractEvent} from '../../../settings/model/extract-event';
import {SendEvent} from '../../../settings/model/send-event';
import {SendPackage} from '../../../settings/model/send-package';
import {ExtractDatabaseProtocol} from '../../../settings/model/extract-protocol';
import {LoadFromEmrCommand} from '../../../settings/model/load-from-emr-command';
import {ExtractProfile} from '../../ndwh-docket/model/extract-profile';
import {CentralRegistry} from '../../../settings/model/central-registry';
import {SendResponse} from '../../../settings/model/send-response';
import {EmrConfigService} from '../../../settings/services/emr-config.service';
import {LoadExtracts} from '../../../settings/model/load-extracts';

@Component({
  selector: 'liveapp-hts-console',
  templateUrl: './hts-console.component.html',
  styleUrls: ['./hts-console.component.scss']
})
export class HtsConsoleComponent implements OnInit, OnDestroy, OnChanges {
    @Input() emr: EmrSystem;
    @Input() emrVer: string;
    private _hubConnection: HubConnection | undefined;
    private _sendhubConnection: HubConnection | undefined;
    public async: any;

    public emrName: string;
    public emrVersion: string;

    private _confirmationService: ConfirmationService;
    private _ndwhExtractService: HtsService;
    private _registryConfigService: RegistryConfigService;
    private _ndwhSenderService: HtsSenderService;

    public load$: Subscription;
    public loadRegistry$: Subscription;
    public send$: Subscription;
    public getStatus$: Subscription;
    public sendManifest$: Subscription;

    public loadingData: boolean;
    public extracts: Extract[] = [];
    public currentExtract: Extract;
    private dwhExtract: DwhExtract;
    private dwhExtracts: DwhExtract[] = [];
    private extractEvent: ExtractEvent;
    public sendEvent: SendEvent = {};
    public recordCount: number;

    public canLoadFromEmr: boolean;
    public canSend: boolean;
    public canSendPatients: boolean = false;
    public manifestPackage: SendPackage;
    public patientPackage: SendPackage;
    public sending: boolean = false;
    public sendingManifest: boolean = false;

    public errorMessage: Message[];
    public otherMessage: Message[];
    public notifications: Message[];
    private _extractDbProtocol: ExtractDatabaseProtocol;
    private _extractDbProtocols: ExtractDatabaseProtocol[];
    private extractLoadCommand: LoadFromEmrCommand;
    private loadExtractsCommand: LoadExtracts;
    private extractPatient: ExtractProfile;
    private extractPatientArt: ExtractProfile;
    private extractPatientBaseline: ExtractProfile;
    private extractPatientLaboratory: ExtractProfile;
    private extractPatientPharmacy: ExtractProfile;
    private extractPatientStatus: ExtractProfile;
    private extractPatientVisit: ExtractProfile;
    private extractProfile: ExtractProfile;
    private extractProfiles: ExtractProfile[] = [];
    public centralRegistry: CentralRegistry;
    public sendResponse: SendResponse;
    public getEmr$: Subscription;

    public constructor(
        confirmationService: ConfirmationService,
        emrConfigService: HtsService,
        registryConfigService: RegistryConfigService,
        psmartSenderService: HtsSenderService,
        private emrService: EmrConfigService
    ) {
        this._confirmationService = confirmationService;
        this._ndwhExtractService = emrConfigService;
        this._registryConfigService = registryConfigService;
        this._ndwhSenderService = psmartSenderService;
    }

    public ngOnChanges(changes: { [propKey: string]: SimpleChange }) {
        this.loadData();
    }

    public ngOnInit() {
        this.loadRegisrty();
        this.liveOnInit();
        this.loadData();
    }

    public loadData(): void {
        this.canLoadFromEmr = this.canSend = false;

        if (this.emr) {
            this.canLoadFromEmr = true;
            this.loadingData = true;
            this.recordCount = 0;
            this.extracts = this.emr.extracts.filter(
                x => x.docketId === 'NDWH'
            );
            this.updateEvent();
            this.emrName = this.emr.name;
            this.emrVersion = `(Ver. ${this.emr.version})`;
        }
        if (this.centralRegistry) {
            this.canSend = true;
        }
    }

    public loadFromEmr(): void {
        this.errorMessage = [];
        this.load$ = this._ndwhExtractService
            .extractAll(this.generateExtractLoadCommand(this.emr))
            .subscribe(
                p => {
                    // this.isVerfied = p;
                },
                e => {
                    this.errorMessage = [];
                    this.errorMessage.push({
                        severity: 'error',
                        summary: 'Error verifying ',
                        detail: <any>e
                    });
                },
                () => {
                    this.errorMessage.push({
                        severity: 'success',
                        summary: 'load was successful '
                    });
                    this.updateEvent();
                }
            );
    }

    public loadRegisrty(): void {
        this.errorMessage = [];
        this.loadRegistry$ = this._registryConfigService.get('NDWH').subscribe(
            p => {
                this.centralRegistry = p;
            },
            e => {
                this.errorMessage = [];
                this.errorMessage.push({
                    severity: 'error',
                    summary: 'Error loading regisrty ',
                    detail: <any>e
                });
            },
            () => {
            }
        );
    }

    public updateEvent(): void {
        this.extracts.forEach(extract => {
            this.getStatus$ = this._ndwhExtractService
                .getStatus(extract.id)
                .subscribe(
                    p => {
                        extract.extractEvent = p;
                        if (extract.extractEvent) {
                            this.canSend = extract.extractEvent.queued > 0;
                        }
                    },
                    e => {
                        this.errorMessage = [];
                        this.errorMessage.push({
                            severity: 'error',
                            summary: 'Error loading status ',
                            detail: <any>e
                        });
                    },
                    () => {
                    }
                );
        });
    }


    public send(): void {
        this.sendingManifest = true;
        this.errorMessage = [];
        this.notifications = [];
        this.canSendPatients = false;
        this.manifestPackage = this.getSendManifestPackage();
        this.sendManifest$ = this._ndwhSenderService.sendManifest(this.manifestPackage)
            .subscribe(
                p => {
                    this.canSendPatients = true;
                },
                e => {
                    this.errorMessage = [];
                    this.errorMessage.push({severity: 'error', summary: 'Error sending ', detail: <any>e});
                },
                () => {
                    this.notifications.push({severity: 'success', summary: 'Manifest sent'});
                    this.sendPatientExtract();
                    this.sendingManifest = false;
                    this.updateEvent();
                }
            );
    }

    public sendPatientExtract(): void {
        this.sendEvent = {sentProgress: 0};
        this.sending = true;
        this.errorMessage = [];
        this.patientPackage = this.getPatientExtractPackage();
        this.send$ = this._ndwhSenderService.sendPatientExtracts(this.patientPackage)
            .subscribe(
                p => {
                    // this.sendResponse = p;
                },
                e => {
                    this.errorMessage = [];
                    this.errorMessage.push({severity: 'error', summary: 'Error sending ', detail: <any>e});
                },
                () => {
                    this.errorMessage.push({severity: 'success', summary: 'sent successfully '});
                    this.sending = false;
                    this.updateEvent();
                }
            );
    }

    private getSendManifestPackage(): SendPackage {
        return {
            destination: this.centralRegistry,
            extractId: this.extracts.find(x => x.name === 'PatientExtract').id
        };
    }

    private getPatientExtractPackage(): SendPackage {
        return {
            destination: this.centralRegistry,
            extractId: this.extracts.find(x => x.name === 'PatientExtract').id
        };
    }


    private liveOnInit() {
        this._hubConnection = new HubConnectionBuilder()
            .withUrl(
                `http://${document.location.hostname}:5757/ExtractActivity`
            )
            .configureLogging(LogLevel.Trace)
            .build();

        this._hubConnection.start().catch(err => console.error(err.toString()));

        this._hubConnection.on('ShowProgress', (extractActivityNotification: any) => {
            this.currentExtract = this.extracts.find(
                x => x.id === extractActivityNotification.extractId
            );
            if (this.currentExtract) {
                this.extractEvent = {
                    lastStatus: `${extractActivityNotification.progress.status}`,
                    found: extractActivityNotification.progress.found,
                    loaded: extractActivityNotification.progress.loaded,
                    rejected: extractActivityNotification.progress.rejected,
                    queued: extractActivityNotification.progress.queued,
                    sent: extractActivityNotification.progress.sent
                };
                this.currentExtract.extractEvent = {};
                this.currentExtract.extractEvent = this.extractEvent;
                const newWithoutPatientExtract = this.extracts.filter(
                    x => x.id !== extractActivityNotification.extractId
                );
                this.extracts = [
                    ...newWithoutPatientExtract,
                    this.currentExtract
                ];
            }
        });

        this._hubConnection.on('ShowDwhSendProgress', (dwhProgress: any) => {
            console.log(dwhProgress);

            this.sendEvent = {
                sentProgress: dwhProgress.progress
            };
            this.sending = true;
            this.canLoadFromEmr = this.canSend = ! this.sending;
        });
    }

    private getExtractProtocols(
        currentEmr: EmrSystem
    ): ExtractDatabaseProtocol[] {
        this._extractDbProtocols = [];
        this.extracts.forEach(e => {
            e.emr = currentEmr.name;
            this._extractDbProtocols.push({
                extract: e,
                databaseProtocol: currentEmr.databaseProtocols[0]
            });
        });
        return this._extractDbProtocols;
    }

    private generateExtractLoadCommand(currentEmr: EmrSystem): LoadExtracts {
        this.extractProfiles.push(this.generateExtractPatient(currentEmr));
        this.extractProfiles.push(this.generateExtractPatientArt(currentEmr));
        this.extractProfiles.push(this.generateExtractPatientBaseline(currentEmr));
        this.extractLoadCommand = {
            extracts: this.extractProfiles
        };

        this.loadExtractsCommand = {
            loadFromEmrCommand: this.extractLoadCommand,
            extractMpi: null,
            loadMpi: null
        };
        return this.loadExtractsCommand;

    }



    private generateExtractPatient(currentEmr: EmrSystem): ExtractProfile {
        const selectedProtocal = this.extracts.find(x => x.name === 'HTSClientExtract').databaseProtocolId;
        this.extractPatient = {
            databaseProtocol: currentEmr.databaseProtocols.filter(x => x.id === selectedProtocal)[0],
            extract: this.extracts.find(x => x.name === 'HTSClientExtract')
        };
        return this.extractPatient;
    }

    private generateExtractPatientArt(currentEmr: EmrSystem): ExtractProfile {
        const selectedProtocal = this.extracts.find(x => x.name === 'HTSClientLinkageExtract').databaseProtocolId;
        this.extractPatientArt = {databaseProtocol: currentEmr.databaseProtocols.filter(x => x.id === selectedProtocal)[0],
            extract: this.extracts.find(x => x.name === 'HTSClientLinkageExtract')
        };
        return this.extractPatientArt;
    }

    private generateExtractPatientBaseline(currentEmr: EmrSystem): ExtractProfile {
        const selectedProtocal = this.extracts.find(x => x.name === 'HTSClientPartnerExtract').databaseProtocolId;
        this.extractPatientBaseline = {databaseProtocol: currentEmr.databaseProtocols.filter(x => x.id === selectedProtocal)[0],
            extract: this.extracts.find(x => x.name === 'HTSClientPartnerExtract')
        };
        return this.extractPatientBaseline;
    }

    private getSendPackage(docketId: string): SendPackage {
        return {
            extractId: this.extracts[0].id,
            destination: this.centralRegistry,
            docket: docketId,
            endpoint: ''
        };
    }

    public ngOnDestroy(): void {
        if (this.load$) {
            this.load$.unsubscribe();
        }
        if (this.loadRegistry$) {
            this.loadRegistry$.unsubscribe();
        }
        if (this.send$) {
            this.send$.unsubscribe();
        }
    }
}
