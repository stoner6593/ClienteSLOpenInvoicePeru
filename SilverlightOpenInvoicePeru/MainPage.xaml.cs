﻿using OpenInvoicePeru.Comun.Dto.Intercambio;
using OpenInvoicePeru.Comun.Dto.Modelos;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SilverlightOpenInvoicePeru
{
    public partial class MainPage : UserControl
    {
        private const string BaseUrl = "http://localhost/OpenInvoicePeru/api";
        private const string UrlSunat = "https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService";
        private const string FormatoFecha = "yyyy-MM-dd";
        private DocumentoElectronico _documento;
        private DocumentoResponse _documentoResponse = new DocumentoResponse();
        private FirmadoResponse _responseFirma = new FirmadoResponse();
        private readonly RestClient _client = new RestClient(BaseUrl);


        public MainPage()
        {
            InitializeComponent();
        }

        private Contribuyente CrearEmisor()
        {
            return new Contribuyente
            {
                NroDocumento = "20100070970",
                TipoDocumento = "6",
                Direccion = "CAL.MORELLI NRO. 181 INT. P-2",
                Urbanizacion = "-",
                Departamento = "LIMA",
                Provincia = "LIMA",
                Distrito = "SAN BORJA",
                NombreComercial = "PLAZA VEA",
                NombreLegal = "SUPERMERCADOS PERUANOS SOCIEDAD ANONIMA",
                Ubigeo = "140101"
            };
        }

        private void CrearFactura()
        {

            try
            {
                _documento = new DocumentoElectronico
                {
                    Emisor = CrearEmisor(),
                    Receptor = new Contribuyente
                    {
                        NroDocumento = "20100039207",
                        TipoDocumento = "6",
                        NombreLegal = "RANSA COMERCIAL S.A."
                    },
                    IdDocumento = "FF11-001",
                    FechaEmision = DateTime.Today.AddDays(-5).ToString(FormatoFecha),
                    Moneda = "PEN",
                    MontoEnLetras = "SON CIENTO DIECIOCHO SOLES CON 0/100",
                    CalculoIgv = 0.18m,
                    CalculoIsc = 0.10m,
                    CalculoDetraccion = 0.04m,
                    TipoDocumento = "01",
                    TotalIgv = 18,
                    TotalVenta = 118,
                    Gravadas = 100,
                    Items = new List<DetalleDocumento>
                    {
                        new DetalleDocumento
                        {
                            Id = 1,
                            Cantidad = 5,
                            PrecioReferencial = 20,
                            PrecioUnitario = 20,
                            TipoPrecio = "01",
                            CodigoItem = "1234234",
                            Descripcion = "Arroz Costeño",
                            UnidadMedida = "KG",
                            Impuesto = 18,
                            TipoImpuesto = "10", // Gravada
                            TotalVenta = 100,
                            Suma = 100
                        }
                    }
                };

                TxtResultado.Text = "Generando XML....";

                var requestInvoice = new RestRequest("GenerarFactura", Method.POST)
                {
                    RequestFormat = DataFormat.Json
                };

                requestInvoice.AddBody(_documento);

                _client.ExecuteAsync<DocumentoResponse>(requestInvoice,
                    response =>
                    {
                        _documentoResponse = response.Data;

                        Dispatcher.BeginInvoke(() =>
                        {
                            TxtResultado.Text = _documentoResponse.Exito 
                                ? "XML Generado" : _documentoResponse.MensajeError;
                            BtnFirmar.IsEnabled = _documentoResponse.Exito;
                        });
                        
                    });
            }
            catch (Exception ex)
            {
                TxtResultado.Text = ex.Message;
            }
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            CrearFactura();
        }

        private void BtnFirmar_OnClick(object sender, RoutedEventArgs e)
        {
            TxtResultado.Text = "Firmando XML...";
            
            var resourceStream = Application.GetResourceStream(new Uri("certificado.pfx", UriKind.Relative));
            var arrayBytes = new byte[resourceStream.Stream.Length];
            resourceStream.Stream.Read(arrayBytes, 0, arrayBytes.Length);
            var content = Convert.ToBase64String(arrayBytes);
            
            // Firmado del Documento.
            var firmado = new FirmadoRequest
            {
                TramaXmlSinFirma = _documentoResponse.TramaXmlSinFirma,
                CertificadoDigital = content,
                PasswordCertificado = string.Empty,
                UnSoloNodoExtension = false
            };

            var requestFirma = new RestRequest("Firmar", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };
            requestFirma.AddBody(firmado);

            _client.ExecuteAsync<FirmadoResponse>(requestFirma, response =>
            {
                _responseFirma = response.Data;

                Dispatcher.BeginInvoke(() =>
                {
                    TxtResultado.Text = _responseFirma.Exito ? 
                        "XML Firmado" : _responseFirma.MensajeError;
                    BtnEnviar.IsEnabled = _responseFirma.Exito;
                });
            });
        }

        private void BtnEnviar_OnClick(object sender, RoutedEventArgs e)
        {
            var sendBill = new EnviarDocumentoRequest
            {
                Ruc = _documento.Emisor.NroDocumento,
                UsuarioSol = "MODDATOS",
                ClaveSol = "MODDATOS",
                EndPointUrl = UrlSunat,
                IdDocumento = _documento.IdDocumento,
                TipoDocumento = _documento.TipoDocumento,
                TramaXmlFirmado = _responseFirma.TramaXmlFirmado
            };

            var requestSendBill = new RestRequest("EnviarDocumento", Method.POST)
            {
                RequestFormat = DataFormat.Json
            };
            requestSendBill.AddBody(sendBill);

            _client.ExecuteAsync<EnviarDocumentoResponse>(requestSendBill, response =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    TxtResultado.Text = response.Data.MensajeRespuesta;
                });
            });

        }
    }
}